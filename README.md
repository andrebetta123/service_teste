# UPS Solution — Full (.NET Framework 4.8)

Este bundle contém **toda a solução** pronta para abrir no Visual Studio 2019 (PT‑BR):

- **ups_Entities** (POCOs)
- **ups_Common** (Db + OperationResult)
- **ups_DAO** (ADO.NET puro)
- **ups_Business** (regras + execução de jobs + scheduler)
- **ups_Work_Job_API** (ASP.NET Web API 4.8, IIS Express, Swagger 5.x)
- **ups_Work_Job_Service** (Windows Service .NET 4.8)
- **ups_Solution.sln** (Debug/Release | Any CPU)

## Como executar a API
1. Abra `ups_Solution.sln`.
2. Projeto **ups_Work_Job_API** → **Definir como Projeto de Inicialização**.
3. **Restaurar pacotes** (NuGet) se necessário.
4. **F5**. Teste rotas: `/api/jobs`, `/api/schedules/due`, `/swagger`.

## Serviço Windows
1. Compile **Release**.
2. Instale via `InstallUtil` ou `sc.exe`.
