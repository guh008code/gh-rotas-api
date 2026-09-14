# GH Rotas API
API ASP.NET Core 8 para cadastro e autenticação de site e aplicativo.

## Banco e configuração
Execute **um** dos scripts no banco previamente criado:
- SQL Server: `scripts/sqlserver.sql`
- MySQL 8+: `scripts/mysql.sql`

A API não cria o banco nem executa scripts automaticamente. Defina `Database:Provider` como `SqlServer` ou `MySql`. A implementação usa consultas parametrizadas e índice único para impedir cadastros concorrentes com o mesmo e-mail. O e-mail é comparado sem diferenciar maiúsculas de minúsculas.

Na pasta `gh-rotas-api`, configure os segredos locais:
```powershell
dotnet user-secrets set "Jwt:Key" "<segredo-aleatorio-com-pelo-menos-32-bytes>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=GhRotas;Integrated Security=true;Encrypt=true;TrustServerCertificate=true"
```
A connection string acima é um exemplo SQL Server local. Em produção use certificado válido e `TrustServerCertificate=false`.
Para MySQL:
```powershell
dotnet user-secrets set "Database:Provider" "MySql"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=gh_rotas;User ID=gh_rotas;Password=<senha>;SslMode=VerifyFull"
```
Gere a chave com um gerador criptográfico (por exemplo, `[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))`). Não use o texto de exemplo como chave.

Em produção, injete `Jwt__Key`, `Database__Provider` e `ConnectionStrings__DefaultConnection` por variáveis de ambiente ou gerenciador de segredos. Em produção a aplicação recusa iniciar sem configuração. Em Development, o Swagger abre sem conexão; os endpoints de autenticação retornam 503 até configurar o banco. Se Jwt:Key estiver vazia, uma chave aleatória temporária é gerada em memória, e os tokens deixam de valer ao reiniciar. Configure `Cors:AllowedOrigins` com as origens exatas do site (ex.: `https://seusite.com`, sem barra final). Aplicativos nativos não dependem de CORS.

```powershell
dotnet run --project gh-rotas-api
dotnet test tests/Auth.Tests
```
Swagger disponível em `/swagger` no ambiente Development. Use o botão Authorize com o accessToken.

## Endpoints
### POST /api/auth/cadastro
```json
{
  "name": "Ana Silva",
  "email": "ana@example.com",
  "phone": "+55 11 99999-1234",
  "password": "Senha-forte-123!",
  "birthDate": "1995-05-12"
}
```
Retorna 201 com `{ id, name, email, phone }`. 400 para dados inválidos e 409 para e-mail já cadastrado.
Senha: 12 a 128 caracteres; nome até 150; e-mail até 254; telefone até 25 caracteres, mínimo 8 dígitos. Data obrigatória, de 1900 até hoje (UTC).

### POST /api/auth/login
```json
{ "email": "ana@example.com", "password": "Senha-forte-123!" }
```
Retorna 200:
```json
{
  "accessToken": "<jwt>",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "user": { "id": "<uuid>", "name": "Ana Silva", "email": "ana@example.com", "phone": "+55 11 99999-1234" }
}
```
Senha ou e-mail incorretos retornam 401 com a mesma mensagem.

### GET /api/auth/perfil
Envie `Authorization: Bearer <accessToken>`.
Retorna `{ id, name, email, phone }` consultado no banco. Sem token, token inválido/expirado ou usuário removido: 401.

## Operação
Senhas usam PasswordHasher do ASP.NET Core (PBKDF2 com salt); nenhum endpoint retorna o hash. JWT valida assinatura, emissor, destinatário e expiração, conforme a [documentação Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication). Expiração configurável entre 1 e 60 minutos, padrão 15, com tolerância de relógio de 30 segundos.

Há limite geral de 120 requisições/minuto por IP e 32 requisições simultâneas por instância. Cadastro e login compartilham limite de 10 requisições/minuto por IP; login também permite 5 tentativas por e-mail a cada 5 minutos. Ao exceder: 429 com Retry-After. Veja SECURITY.md para detalhes e riscos restantes. Em múltiplas instâncias, aplique também limite no gateway. Atrás de proxy, configure encaminhamento de IP/HTTPS somente para proxies confiáveis; sem isso clientes podem compartilhar o limite do proxy.

Publique somente por HTTPS. No aplicativo guarde o token no armazenamento seguro do sistema; no site prefira memória ou uma camada BFF com cookie HttpOnly. Token é uma credencial e não deve ser registrado em logs.

O perfil solicitado representa a identidade do usuário; não há papéis administrativos ou permissões adicionais. Não há refresh token, logout com revogação, recuperação de senha ou confirmação de e-mail nesta versão. Ao expirar, faça login novamente; remover o token no cliente não o revoga antes da expiração.

Os testes HTTP usam repositório em memória, cobrem cadastro/login/perfil, duplicidade, validação, token inválido e limite de requisições. A validação real dos scripts e conectividade depende de uma instância SQL Server/MySQL configurada.



## Estrutura MVC e SOLID
A API usa controllers do ASP.NET Core MVC e retorna JSON, portanto não precisa de Views Razor.

- Controllers: recebem requisições, identificam o usuário autenticado e retornam os códigos HTTP.
- Models: entidade User, modelos de entrada em Requests e modelos de saída em Responses, um tipo por arquivo.
- Services: AuthService implementa cadastro, login e consulta de perfil; TokenService emite JWT.
- Repositories: UserRepository encapsula consultas e gravações SQL.
- Program.cs: configura a aplicação e registra as implementações na injeção de dependência.

O controller depende de IAuthService. O serviço depende de IUserRepository, ITokenService e IPasswordHasher<User>. As interfaces pequenas separam responsabilidades e permitem substituir banco, emissão de token ou serviço sem alterar o controller. A entidade User não depende dos modelos de resposta HTTP.

As rotas atuais são POST /api/auth/cadastro, POST /api/auth/login e GET /api/auth/perfil. As rotas anteriores /api/auth/register e /api/auth/me foram substituídas; atualize os clientes.

## Testar pelo Swagger
Para visualizar os endpoints, execute pelo Visual Studio com F5 ou Ctrl+F5. Os perfis de execução já abrem a página swagger no navegador.

Pelo terminal:
```powershell
dotnet run --project gh-rotas-api --launch-profile https
```
Abra https://localhost:7229/swagger. No perfil http, use http://localhost:5232/swagger. O comando dotnet run não abre o navegador automaticamente.

1. Execute POST /api/auth/cadastro preenchendo o corpo JSON.
2. Execute POST /api/auth/login e copie o accessToken da resposta.
3. Clique em Authorize e cole apenas o token.
4. Execute GET /api/auth/perfil. O Swagger adiciona o cabeçalho Bearer.

O Swagger está habilitado em Development. Em Development é possível abrir o Swagger sem conexão nem chave JWT configuradas. Os endpoints de autenticação precisam do banco e da tabela criados; até configurar a conexão e reiniciar, retornam 503 com orientação. Em produção, conexão e chave JWT continuam obrigatórias.

## Formatação
O arquivo .editorconfig padroniza a indentação C# em quatro espaços e os arquivos de configuração em dois espaços. Para aplicar o padrão:
```powershell
dotnet format whitespace gh-rotas-api.sln --no-restore
```



## Credenciais e publicação
appsettings.json contém somente configurações públicas; conexão e chave JWT ficam vazias.
Para desenvolvimento, use **Gerenciar Segredos do Usuário** no Visual Studio. O projeto usa o identificador gh-rotas-api-auth.

Exemplo de estrutura local (mescle com os demais segredos existentes):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=SEU_SERVIDOR;Database=SEU_BANCO;User ID=SEU_USUARIO;Password=SUA_SENHA;Encrypt=True;TrustServerCertificate=False"
  },
  "Jwt": {
    "Key": "SUBSTITUA_POR_UM_SEGREDO_ALEATORIO"
  }
}
```

Segredos do Usuário ficam fora do repositório e não são criptografados pelo .NET: proteja o acesso à sua conta local.
Em produção, injete ConnectionStrings__DefaultConnection e Jwt__Key usando os segredos da hospedagem. Nunca coloque credenciais em commits, prints, arquivos .http ou logs.
O .gitignore exclui arquivos locais, artefatos de compilação, certificados, logs e backups.
