# Easy Frete

Ferramenta de automação e apoio operacional para consulta de produtos, sincronização de tabelas tarifárias e cálculo ágil de fretes.

> **Aviso:** Este projeto **não possui fins comerciais**. Trata-se de uma automação desenvolvida exclusivamente para auxiliar na rotina operacional e resolver problemas práticos do dia a dia.

---

## Para que serve

Na operação diária de logística e e-commerce, calcular fretes corretamente exige cruzar informações de peso, dimensões de produtos (SKUs) e faixas tarifárias que mudam com frequência. O **Easy Frete** centraliza esse processo em uma interface web simples e uma API dedicada, eliminando consultas manuais em planilhas e reduzindo erros operacionais.

### Principais funcionalidades

- **Cálculo de frete:** Simulação rápida de valores de frete a partir de SKUs, características físicas (peso e dimensões) e tabelas tarifárias vigentes.
- **Gestão e consulta de produtos:** Listagem e busca de SKUs cadastrados com atualização em lote a partir de fontes externas.
- **Sincronização de tarifas e catálogo:** Endpoints protegidos por chave de API para ingestão automatizada de tabelas de preços e dados de produtos (como integrações via planilhas).
- **Controle de acesso:** Autenticação baseada em token, gerenciamento de perfis (usuário e administrador) e aprovação manual de novos cadastros.

---

## Stack utilizada

### Back-end
- C# / .NET 10 (Minimal APIs)
- Entity Framework Core
- SQLite
- JWT
- NUnit

### Front-end
- React
- TypeScript
- Tailwind CSS v4
- Vite
- Bun
- Oxlint

### Infraestrutura e Automação
- Docker & Docker Compose
- Nginx
- Google Apps Script
