
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
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

        public void Start() => Task.Run(() => LoopAsync(_cts.Token));
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
                        await _parallel.WaitAsync(ct).ConfigureAwait(false);

                        _ = Task.Run(async () =>
                        {
                            try { await ProcessOneScheduleAsync(s).ConfigureAwait(false); }
                            catch (Exception ex) { Trace.TraceError($"Erro no processamento do schedule {s.ScheduleId}: {ex}"); }
                            finally { _parallel.Release(); }

                        }, ct);
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
            using (var conn = new SqlConnection(GetConn()))
            using (var cmd = new SqlCommand(@"
SELECT TOP (@take)
       s.ScheduleId, s.JobId, s.NextRunUtc, s.Enabled,
       j.Name, j.Enabled AS JobEnabled, j.MaxRetries, j.RetryDelaySec, j.ConcurrencyKey
FROM dbo.JobSchedules s
JOIN dbo.Jobs j ON j.JobId = s.JobId
WHERE s.Enabled = 1
  AND j.Enabled = 1
  AND s.NextRunUtc IS NOT NULL
  AND s.NextRunUtc <= SYSUTCDATETIME()
ORDER BY s.NextRunUtc ASC, s.JobId ASC;", conn))
            {
                cmd.Parameters.Add("@take", SqlDbType.Int).Value = take;
                await conn.OpenAsync().ConfigureAwait(false);
                using (var rd = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await rd.ReadAsync().ConfigureAwait(false))
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
            }
            return list;
        }

        private string GetConn() =>
            ConfigurationManager.ConnectionStrings[ConfigurationManager.AppSettings["DbConnName"] ?? "Default"].ConnectionString;

        private async Task ProcessOneScheduleAsync(DueSchedule s)
        {
            // 1) Tentar garantir lock (sp_getapplock) para evitar concorrência do MESMO Job
            using (var conn = new SqlConnection(GetConn()))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    if (!Locking.TryAcquireJobLock(conn, tx, s.ConcurrencyKey, s.JobId))
                    {
                        tx.Rollback(); // outro worker pegou
                        return;
                    }

                    // marcar que avaliou agora
                    using (var upd = new SqlCommand(
                        "UPDATE dbo.JobSchedules SET LastEvaluatedUtc = SYSUTCDATETIME() WHERE ScheduleId = @id", conn, tx))
                    {
                        upd.Parameters.Add("@id", SqlDbType.Int).Value = s.ScheduleId;
                        upd.ExecuteNonQuery();
                    }

                    tx.Commit();
                }
            }

            // 2) Executar o Job (com retry/backoff por step)
            bool ok = await JobExecutor.RunJobAsync(s.JobId, s.MaxRetries, s.RetryDelaySec).ConfigureAwait(false);

            // 3) Recalcular próxima execução (UTC)
            DateTime? nextUtc = await NextRunCalculator.ComputeNextRunUtcAsync(s.ScheduleId, s.JobId).ConfigureAwait(false);

            // 4) Atualizar agenda com próxima execução
            using (var conn = new SqlConnection(GetConn()))
            using (var cmd = new SqlCommand(
                "UPDATE dbo.JobSchedules SET NextRunUtc = @next, LastEvaluatedUtc = SYSUTCDATETIME() WHERE ScheduleId = @sid", conn))
            {
                cmd.Parameters.Add("@next", SqlDbType.DateTime2).Value = (object)nextUtc ?? DBNull.Value;
                cmd.Parameters.Add("@sid", SqlDbType.Int).Value = s.ScheduleId;
                await conn.OpenAsync().ConfigureAwait(false);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            Trace.TraceInformation($"Job {s.JobId} executado. Status={(ok ? "SUCCESS" : "FAILED")}. Próximo={nextUtc?.ToString("u") ?? "null"}");
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