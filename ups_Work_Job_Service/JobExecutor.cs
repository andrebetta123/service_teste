
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ups_Work_Job_Service
{
    internal static class JobExecutor
    {
        public static async Task<bool> RunJobAsync(int jobId, int maxRetries, int retryDelaySec)
        {
            long runId = await InsertRunHistoryAsync(jobId, "RUNNING", null).ConfigureAwait(false);
            bool ok = false;
            string finalMsg = null;

            try
            {
                var steps = await LoadStepsAsync(jobId).ConfigureAwait(false);
                foreach (var st in steps)
                {
                    ok = await ExecuteStepWithRetryAsync(st, maxRetries, retryDelaySec).ConfigureAwait(false);
                    if (!ok) { finalMsg = $"Falha no Step {st.StepNo} (StepId={st.StepId})"; break; }
                }
                finalMsg = ok ? "Executado com sucesso" : finalMsg ?? "Falha desconhecida";
                await FinishRunHistoryAsync(runId, ok ? "SUCCESS" : "FAILED", finalMsg).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ok = false;
                finalMsg = $"Exceção no Job {jobId}: {ex.Message}";
                await FinishRunHistoryAsync(runId, "FAILED", finalMsg).ConfigureAwait(false);
            }

            await UpdateJobLastRunAsync(jobId, ok ? "SUCCESS" : "FAILED").ConfigureAwait(false);
            return ok;
        }

        private static async Task<List<JobStep>> LoadStepsAsync(int jobId)
        {
            var list = new List<JobStep>();
            using (var conn = new SqlConnection(GetConn()))
            using (var cmd = new SqlCommand(
                "SELECT StepId, JobId, StepNo, Script, TimeoutSec FROM dbo.JobSteps WHERE JobId=@jid ORDER BY StepNo ASC", conn))
            {
                cmd.Parameters.Add("@jid", SqlDbType.Int).Value = jobId;
                await conn.OpenAsync().ConfigureAwait(false);
                using (var rd = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await rd.ReadAsync().ConfigureAwait(false))
                    {
                        list.Add(new JobStep
                        {
                            StepId = rd.GetInt32(0),
                            JobId = rd.GetInt32(1),
                            StepNo = rd.GetInt32(2),
                            Script = rd.GetString(3),
                            TimeoutSec = rd.IsDBNull(4) ? (int?)null : rd.GetInt32(4)
                        });
                    }
                }
            }
            return list;
        }

        private static async Task<bool> ExecuteStepWithRetryAsync(JobStep st, int maxRetries, int retryDelaySec)
        {
            int attempts = 0;
            while (true)
            {
                attempts++;
                try
                {
                    using (var conn = new SqlConnection(GetConn()))
                    using (var cmd = new SqlCommand(st.Script, conn))
                    {
                        cmd.CommandTimeout = st.TimeoutSec ?? DefaultTimeout();
                        await conn.OpenAsync().ConfigureAwait(false);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                    return true;
                }
                catch (SqlException ex) when (IsTransient(ex) && attempts < maxRetries)
                {
                    Trace.TraceWarning($"Step {st.StepNo} (Job {st.JobId}) erro transitório: {ex.Message}. Tentativa {attempts}/{maxRetries}.");
                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySec)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Step {st.StepNo} (Job {st.JobId}) falhou: {ex}");
                    return false;
                }
            }
        }

        private static async Task<long> InsertRunHistoryAsync(int jobId, string status, string message)
        {
            using (var conn = new SqlConnection(GetConn()))
            using (var cmd = new SqlCommand(
                "INSERT INTO dbo.JobRunHistory (JobId, Status, Message, HostName) VALUES (@jid,@st,@msg,@host); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", conn))
            {
                cmd.Parameters.Add("@jid", SqlDbType.Int).Value = jobId;
                cmd.Parameters.Add("@st", SqlDbType.NVarChar, 50).Value = status;
                cmd.Parameters.Add("@msg", SqlDbType.NVarChar).Value = (object)message ?? DBNull.Value;
                cmd.Parameters.Add("@host", SqlDbType.NVarChar, 200).Value = Environment.MachineName;
                await conn.OpenAsync().ConfigureAwait(false);
                var runId = (long)await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                return runId;
            }
        }

        private static async Task FinishRunHistoryAsync(long runId, string status, string message)
        {
            using (var conn = new SqlConnection(GetConn()))
            using (var cmd = new SqlCommand(
                "UPDATE dbo.JobRunHistory SET FinishedUtc=SYSUTCDATETIME(), Status=@st, Message=@msg WHERE RunId=@rid;", conn))
            {
                cmd.Parameters.Add("@rid", SqlDbType.BigInt).Value = runId;
                cmd.Parameters.Add("@st", SqlDbType.NVarChar, 50).Value = status;
                cmd.Parameters.Add("@msg", SqlDbType.NVarChar).Value = (object)message ?? DBNull.Value;
                await conn.OpenAsync().ConfigureAwait(false);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        private static async Task UpdateJobLastRunAsync(int jobId, string status)
        {
            using (var conn = new SqlConnection(GetConn()))
            using (var cmd = new SqlCommand(
                "UPDATE dbo.Jobs SET LastRunUtc=SYSUTCDATETIME(), LastRunStatus=@st WHERE JobId=@jid;", conn))
            {
                cmd.Parameters.Add("@jid", SqlDbType.Int).Value = jobId;
                cmd.Parameters.Add("@st", SqlDbType.NVarChar, 50).Value = status;
                await conn.OpenAsync().ConfigureAwait(false);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        private static string GetConn() =>
            ConfigurationManager.ConnectionStrings[ConfigurationManager.AppSettings["DbConnName"] ?? "Default"].ConnectionString;

        private static int DefaultTimeout() =>
            int.TryParse(ConfigurationManager.AppSettings["DefaultCommandTimeoutSec"], out var s) ? s : 60;

        private static bool IsTransient(SqlException ex)
        {
            // exemplos comuns: -2 (timeout), 4060 (db indisponível), 10928/10929 (throttling), 40197/40501 (Azure)
            var codes = new[] { -2, 4060, 10928, 10929, 40197, 40501 };
            return codes.Contains(ex.Number);
        }
    }

    internal sealed class JobStep
    {
        public int StepId { get; set; }
        public int JobId { get; set; }
        public int StepNo { get; set; }
        public string Script { get; set; }
        public int? TimeoutSec { get; set; }
    }
}
