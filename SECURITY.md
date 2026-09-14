# Revisão de segurança — 14/09/2026

## Implementado
- Limite geral: 120 requisições/minuto por IP, inclusive perfil e requisições sem token.
- Limite de cadastro/login: 10 requisições/minuto por IP, compartilhadas.
- Login: 5 tentativas por e-mail normalizado a cada 5 minutos, incluindo tentativas bem-sucedidas e contas inexistentes. Independente do IP. Não bloqueia permanentemente a conta.
- Máximo de 32 requisições simultâneas por instância, sem fila.
- Respostas 429 com Retry-After e mensagem genérica.
- Corpo de cadastro/login limitado a 16 KiB pelo MVC/servidor.
- Autenticação obrigatória por padrão; cadastro e login explicitamente públicos.
- Respostas /api sem cache; cabeçalho nosniff.
- Senhas com PBKDF2 e salt; consultas SQL parametrizadas; índice único para e-mail.
- JWT valida assinatura, algoritmo, expiração, emissor e destinatário; perfil usa o id do token.
- Login retorna a mesma mensagem para conta inexistente e senha incorreta.
- Swagger e chave temporária somente em Development; configuração obrigatória em produção.

## Limites e riscos que permanecem
Esta é uma revisão de código e testes locais, não um pentest da infraestrutura publicada nem garantia de ausência de invasões.

1. Todos os limites estão em memória por processo. Reiniciar zera os contadores; múltiplas réplicas não compartilham limites. Antes de escalar, aplicar controle distribuído no gateway/WAF ou Redis. Os valores precisam de teste de carga para o hardware e tráfego reais.
2. Rate limit não impede DDoS volumétrico. Proteger a borda e restringir acesso direto ao servidor.
3. Por padrão, cabeçalhos X-Forwarded-For enviados pelo cliente não são usados. Atrás de proxy, configurar somente proxies confiáveis e testar o IP resultante; clientes podem compartilhar o mesmo limite. Nunca confiar indiscriminadamente nesses cabeçalhos.
4. O limite por conta pode ser usado para atrasar o login da vítima por até cinco minutos. CAPTCHA adaptativo/MFA e monitoramento são evoluções recomendadas para ataques distribuídos.
5. Cadastro retorna 409 para e-mail existente: permite identificar contas. Isso preserva o contrato atual. Um fluxo com confirmação de e-mail e resposta uniforme reduz essa exposição.
6. Não há MFA, verificação de e-mail, detecção de senhas vazadas ou revogação imediata de JWT. Tokens roubados continuam válidos até expirar (15 minutos por padrão). Usar armazenamento seguro no aplicativo e evitar armazenamento persistente acessível a JavaScript no site.
7. Exigir HTTPS na borda, certificado válido, credenciais de banco com privilégio mínimo, segredos aleatórios fora do repositório, CORS restrito e ambiente Production. Redirecionamento HTTP não protege uma senha que o cliente já enviou por HTTP.
8. Banco real, permissões SQL, TLS, firewall, backups, sistema operacional e exposição de portas ainda não foram auditados. Nenhuma instância SQL Server/MySQL foi disponibilizada.
9. Manter SDK/runtime e pacotes atualizados. A consulta NuGet atual não reportou dependências vulneráveis; isso não cobre falhas desconhecidas nem o runtime instalado.
10. Adicionar observabilidade na implantação (429, falhas de login e erros), sem registrar senhas ou tokens.

## Evidência
Testes locais cobrem cadastro/login/perfil, entradas inválidas, e-mail duplicado, JWT inválido/expirado/assinatura ou claims incorretas, inicialização, Swagger, limites por IP/e-mail e tentativa de burlar o IP via X-Forwarded-For.
A validação de SQL injection foi por inspeção das consultas parametrizadas; não foi executado ataque contra banco real. Concorrência e limite do corpo precisam também de teste em Kestrel/IIS na implantação.

Referências:
- [Rate limiting — Microsoft](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
- [Autenticação e limitação de login — OWASP](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
