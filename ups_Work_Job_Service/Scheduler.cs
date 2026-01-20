
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using ups_Common;
using ups_Entities;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ups_Work_Job_Service
{
    internal sealed class Scheduler
    {
        private readonly int _pollIntervalSec;
        private readonly int _maxParallel;
        private readonly string _connName;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly SemaphoreSlim _parallel;

        public Scheduler(int pollIntervalSec, int maxParallel, string connName)
        {
            _pollIntervalSec = Math.Max(1, pollIntervalSec);
            _maxParallel = Math.Max(1, maxParallel);
            _connName = connName ?? "Default";
            _parallel = new SemaphoreSlim(_maxParallel);
        }

        // public void Start() => Task.Run(() => LoopAsync(_cts.Token));
        public void Start() { LoopAsync(_cts.Token); }
        public void Stop() { _cts.Cancel(); _parallel.Dispose(); }

        private async Task LoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var due = await LoadDueSchedulesAsync(_maxParallel).ConfigureAwait(false);

                    foreach (var s in due)
                    {
                        //await _parallel.WaitAsync(ct).ConfigureAwait(false);

                        //_ = Task.Run(async () =>
                        {
                            try {
                                // await ProcessOneScheduleAsync(s).ConfigureAwait(false);
                                ProcessOneScheduleAsync(s);
                            }
                            catch (Exception ex) { 
                                Trace.TraceError($"Erro no processamento do schedule {s.ScheduleId}: {ex}"); 
                            }
                            finally { _parallel.Release(); }

                        }
                        //, ct);
                    }
                }
                catch (OperationCanceledException) { /* encerrando */ }
                catch (Exception ex)
                {
                    Trace.TraceError($"Erro no loop do scheduler: {ex}");
                }

                try { await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSec), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { /* encerrou */ }
            }
        }

        private async Task<List<DueSchedule>> LoadDueSchedulesAsync(int take)
        {
            var list = new List<DueSchedule>();
//            SELECT TOP(@take)
            using (var conn2 = new SqlConnection(GetConn()))
            using (var cmd = new SqlCommand(@" Select 
                                               s.ScheduleId, s.JobId, coalesce(s.NextRunUtc,getdate()) NextRunUtc, s.Enabled,
                                               j.Name, j.Enabled AS JobEnabled, j.MaxRetries, j.RetryDelaySec, j.ConcurrencyKey
                                        FROM dbo.JobSchedules s
                                        JOIN dbo.Jobs j ON j.JobId = s.JobId
                                        WHERE s.Enabled = 1
                                          AND j.Enabled = 1
                                          AND COALESCE(s.NextRunUtc, dateadd(day,-1,getdate())) <= getdate()
                                        ORDER BY s.NextRunUtc ASC, s.JobId ASC;", conn2))
            {
                // cmd.Parameters.Add("@take", SqlDbType.Int).Value = take;

                // Execução síncrona
                conn2.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        list.Add(new DueSchedule
                        {
                            ScheduleId = rd.GetInt32(0),
                            JobId = rd.GetInt32(1),
                            NextRunUtc = rd.GetDateTime(2),
                            Enabled = rd.GetBoolean(3),
                            Name = rd.GetString(4),
                            JobEnabled = rd.GetBoolean(5),
                            MaxRetries = rd.GetInt32(6),
                            RetryDelaySec = rd.GetInt32(7),
                            ConcurrencyKey = rd.IsDBNull(8) ? null : rd.GetString(8)
                        });
                    }
                }
                cmd.Dispose();
                conn2.Close();
                conn2.Dispose();
            }
            return list;
        }

        private string GetConn() =>
            ConfigurationManager.ConnectionStrings[ConfigurationManager.AppSettings["DbConnName"] ?? "SqlServer"].ConnectionString;

        private async Task ProcessOneScheduleAsync(DueSchedule s)
        {
            // 1) Lock + marcar LastEvaluatedUtc
            using (var conn = new SqlConnection(GetConn()))
            {
                conn.Open();

                using (var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    // Tente adquirir lock lógico (sp_getapplock)
                    bool gotLock = Locking.TryAcquireJobLock(conn, tx, s.ConcurrencyKey, s.JobId);
                    Trace.TraceInformation($"[JOB {s.JobId}] TryAcquireJobLock => {gotLock}");
                    if (!gotLock)
                    {
                        tx.Rollback(); // outro worker pegou
                        return;
                    }

                    using (var upd = new SqlCommand(
                        "UPDATE dbo.JobSchedules SET LastEvaluatedUtc = SYSUTCDATETIME() WHERE ScheduleId = @id", conn, tx))
                    {
                        upd.Parameters.Add("@id", SqlDbType.Int).Value = s.ScheduleId;
                        upd.CommandTimeout = 10; // SqlCmdTimeoutSec;
                        int rows = upd.ExecuteNonQuery();

                        if (rows == 0)
                        {
                            tx.Rollback();
                            Trace.TraceWarning($"[JOB {s.JobId}] Nenhuma linha atualizada (ScheduleId={s.ScheduleId}). Abortando.");
                            return;
                        }
                    }

                    tx.Commit();
                }
            }

            // 2) Executar o Job (com retry/backoff por step)
            Trace.TraceInformation($"[JOB {s.JobId}] Iniciando RunJobSync...");
            bool ok = await JobExecutor.RunJobSync(s.JobId, s.MaxRetries, s.RetryDelaySec/*, token?*/).ConfigureAwait(false);
            Trace.TraceInformation($"[JOB {s.JobId}] RunJobSync concluído: {ok}");

            // 3) Recalcular próxima execução (UTC)
            Trace.TraceInformation($"[JOB {s.JobId}] Iniciando ComputeNextRunUtcSync...");
            DateTime? nextUtc = NextRunCalculator
                .ComputeNextRunUtc(s.ScheduleId, s.JobId/*, token?*/);
            Trace.TraceInformation($"[JOB {s.JobId}] ComputeNextRunUtcSync concluído: {nextUtc?.ToString("u") ?? "null"}");

            // 4) UPDATE final (síncrono)
            using (var conn = new SqlConnection(GetConn()))
            using (var cmd = new SqlCommand(
                "UPDATE dbo.JobSchedules SET NextRunUtc = @next, LastEvaluatedUtc = SYSUTCDATETIME() WHERE ScheduleId = @sid", conn))
            {
                cmd.Parameters.Add("@next", SqlDbType.DateTime2).Value = (object)nextUtc ?? DBNull.Value;
                cmd.Parameters.Add("@sid", SqlDbType.Int).Value = s.ScheduleId;
                cmd.CommandTimeout = 30;

                conn.Open();
                int rows = cmd.ExecuteNonQuery();
                Trace.TraceInformation($"[JOB {s.JobId}] UPDATE NextRunUtc rows={rows}");
            }

            Trace.TraceInformation($"[JOB {s.JobId}] FIM. Status={(ok ? "SUCCESS" : "FAILED")}. Próximo={nextUtc?.ToString("u") ?? "null"}");

        }
    }

    internal sealed class DueSchedule
    {
        public int ScheduleId { get; set; }
        public int JobId { get; set; }
        public DateTime NextRunUtc { get; set; }
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public bool JobEnabled { get; set; }
        public int MaxRetries { get; set; }
        public int RetryDelaySec { get; set; }
        public string ConcurrencyKey { get; set; }
    }
}