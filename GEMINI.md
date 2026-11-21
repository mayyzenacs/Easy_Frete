# Regras de Arquitetura e Contenção — Easy Frete

## 1. Stack e Ferramentas Oficiais (Não Substituir)

- **Back-end:** C# / .NET 10 (Minimal APIs), Entity Framework Core, SQLite, NUnit (`backend/EasyFrete.Api.Tests`).
- **Front-end:** React, TypeScript, Tailwind CSS v4, Vite, **Bun** (`bun.lock`, `bun install`, `bun run build`). **Nunca** use `npm` ou `yarn` nem gere `package-lock.json`.
- **Ambiente Local:** `compose.dev.yml` (porta `5000` para API, `5173` para Vite). **Nunca** altere `compose.dev.yml` ao configurar produção.

## 2. Invariantes de Banco de Dados e Domínio (Proibido Quebrar)

- **Chave Primária de `Usuario`:** Sempre `Guid` (gerado com `Guid.CreateVersion7()`). **Nunca** reverta para `int`.
- **Migrations EF Core:** Todas as migrations e o `AppDbContextModelSnapshot.cs` devem ficar exclusivamente em `backend/EasyFrete.Api/Data/Migrations` (sempre use `-o Data/Migrations`). Nunca crie pasta `Migrations/` na raiz do projeto.
- **Autenticação e RBAC:** JWT (HMAC-SHA256) com `RoleClaimType = ClaimTypes.Role`, `PasswordHasher<Usuario>` e aprovação manual de contas (`Status`: `"Pendente"`, `"Aprovado"`, `"Rejeitado"`). **Não** implemente envio ou confirmação por e-mail.
- **Dados Dinâmicos:** **Nunca** insira tabelas de preços, tarifas do Magalu ou medidas de SKUs hardcoded no back-end ou no front-end. Tudo vem do SQLite alimentado pelos scripts do Google Apps Script (`X-Api-Key`).

## 3. Topologia de Produção (Oracle VPS)

- **Domínio:** `https://easyfrete.mayradev.me` atrás da Cloudflare (Proxy / Nuvem Laranja).
- **Proxy Reverso Existente:** Container `nginx_master` na rede Docker externa `main_network` (`172.18.0.0/16`).
- **Isolamento de Rede:** Containers de produção (`easyfrete-api` e `easyfrete-web`) **nunca** publicam `ports:` no host; comunicam-se apenas via `main_network` na porta interna `8080`.
- **Permissões Linux/Docker:** O container .NET roda como usuário não-root `app` (UID `1654`). O volume de dados em produção é `./data:/app/data`.

## 4. Segurança e Higiene de Código

- **Zero Segredos no Código:** Nunca coloque senhas, chaves JWT, `X-Api-Key` ou arquivos `.db` em código-fonte, `HasData()`, Dockerfiles ou imagens Docker. Leia tudo de variáveis de ambiente via `ApiSettings`.
- **Validação Obrigatória:** Após qualquer alteração no back-end, rode `dotnet test` na pasta `backend/` antes de concluir a tarefa.
