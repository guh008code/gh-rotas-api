# Deploy automático da API na EC2

Push em `main` executa testes, publica imagem privada no ECR e atualiza somente
a API via Systems Manager (SSM). Repositório: guh008code/gh-rotas-api.
A configuração AWS e as variáveis GitHub abaixo são obrigatórias antes do primeiro deploy.

## O que permanece na EC2

- Diretório /home/ubuntu/gh-rotas-api e projeto Compose gh-rotas-api.
- compose.yaml com suas portas/configurações locais e .env com as credenciais.
- SQL Server, db-init, rede e volume gh-rotas-api_sqlserver-data.
- O workflow NÃO copia o compose.yaml do GitHub para a EC2 e NÃO executa SQL.
- Um override compose.deploy.yaml guarda a imagem aprovada por digest.
- A API é recriada com --no-deps --no-build. Há breve indisponibilidade.
- A verificação usa /swagger/v1/swagger.json no Development atual. Ela confirma
  resposta HTTP da API, não a conexão SQL nem o fluxo completo de login.
- Falha HTTP dispara tentativa de rollback para a imagem local anterior.
  Interrupção Spot, falta de disco ou falha do Docker podem exigir recuperação manual.

## 1. ECR: registro privado

No console AWS, região Norte da Virgínia (us-east-1):
ECR → Repositórios privados → Criar repositório → nome gh-rotas-api.
Selecione tags imutáveis e criptografia padrão AES-256.
Cada execução publica uma tag exclusiva e o deploy usa o digest imutável.
Em Política de ciclo de vida, aplique aws/ecr-lifecycle.json para reter 10 imagens.
ECR tem cobrança de armazenamento; GitHub Actions depende da franquia do plano.
A retenção pode remover uma imagem antiga ainda em uso numa EC2 parada por muito
tempo; o próximo deploy publica outra imagem. Rollback usa a imagem local.

## 2. Role da EC2 (acesso AWS do servidor)

IAM → Funções → Criar função → Serviço AWS → EC2.
Nome sugerido: gh-rotas-ec2.
Anexe AmazonSSMManagedInstanceCore.
Adicione a política inline de aws/ec2-ecr-read.json, substituindo AWS_ACCOUNT_ID
pelo ID de 12 dígitos da sua conta.
EC2 → instância → Ações → Segurança → Modificar função do IAM → gh-rotas-ec2.
Se já houver uma role, adicione as permissões nela em vez de substituir suas permissões.

No Ubuntu, verifique o agente:
```bash
sudo systemctl status snap.amazon-ssm-agent.amazon-ssm-agent.service --no-pager
```
Se não estiver instalado:
```bash
sudo snap install amazon-ssm-agent --classic
```
Inicie/habilite:
```bash
sudo systemctl enable --now snap.amazon-ssm-agent.amazon-ssm-agent.service
```
Instale a AWS CLI caso ainda não exista:
```bash
sudo snap install aws-cli --classic
aws --version
sudo aws sts get-caller-identity
```
Não execute aws configure nem cadastre access keys: a role da EC2 fornece credenciais.
Docker, Compose, curl e flock devem estar disponíveis; já instalamos Docker/Compose.
SSM precisa de saída HTTPS/443 aos serviços AWS. Não precisa abrir portas de entrada.
Confira que a instância aparece Online nos nós gerenciados do Systems Manager.

## 3. GitHub OIDC: credenciais temporárias

Publique os arquivos deste projeto na branch main pelo seu fluxo Git habitual.
O deploy falhará enquanto as variáveis AWS não estiverem configuradas; não afeta a API atual.

No GitHub → Actions → Identificar OIDC para configurar AWS → Run workflow → main.
Copie SOMENTE o valor sub exibido. O workflow nunca imprime o token.
O formato do sub varia conforme a data/configuração do repositório; use o valor
real para restringir a role exatamente a este repositório e à branch main.

No IAM → Provedores de identidade → Adicionar provedor:
- Tipo OpenID Connect.
- URL https://token.actions.githubusercontent.com
- Público (Audience): sts.amazonaws.com
Se o provedor já existir, reutilize-o.

IAM → Funções → Criar função → Política de confiança personalizada.
Use aws/github-trust.json substituindo AWS_ACCOUNT_ID e
COPIE_O_SUB_EXATO_DO_WORKFLOW_OIDC pelo sub observado.
Nome sugerido: gh-rotas-github-deploy.
Adicione a política inline aws/github-permissions.json substituindo
AWS_ACCOUNT_ID e EC2_INSTANCE_ID pelo ID da instância.
A permissão SSM permite executar comandos administrativos somente nessa instância.
Proteja acesso de escrita à main e revise alterações de workflows/scripts.

## 4. Variáveis do GitHub

Settings → Secrets and variables → Actions → Variables → New repository variable:

| Nome | Valor |
| --- | --- |
| AWS_ROLE_ARN | ARN da função gh-rotas-github-deploy |
| EC2_INSTANCE_ID | ID da sua instância, começando com i- |

Não são necessários senha SQL, chave JWT, chave PEM nem access keys no GitHub.
O workflow assume us-east-1 e repositório ECR gh-rotas-api.
Não configure GitHub Environment sem ajustar a trust policy: isso muda o sub OIDC.

## 5. Primeiro deploy e validação

Mantenha a EC2 ligada, o SQL saudável e a API atual em execução.
GitHub → Actions → Deploy API na AWS → Run workflow → main.
Acompanhe todos os passos até concluir; falha de SSM NÃO conta como deploy bem-sucedido.
Na EC2:
```bash
cd /home/ubuntu/gh-rotas-api
sudo docker compose -p gh-rotas-api -f compose.yaml -f compose.deploy.yaml ps -a
```
Confirme que o SQL Server não foi recriado. Teste cadastro, login e perfil pelo Swagger.
Nos próximos pushes em main, o deploy será automático.

## Operação e recuperação

- EC2 parada: workflow falha antes de publicar a imagem, sem ligar a máquina.
  Depois de ligá-la e aguardar SSM Online, execute Run workflow em main.
- SSM identifica a EC2 pelo ID; mudanças de IP não afetam o deploy.
- O .env e compose.yaml da EC2 não recebem mudanças do repositório automaticamente.
  Alterações de configuração e migrações de banco são tarefas separadas.
- Não execute docker compose down -v: isso remove os dados.
- Após o primeiro deploy, para operações de atualização manual sempre inclua
  -f compose.yaml -f compose.deploy.yaml; apenas compose.yaml aponta à imagem local antiga.
- Para reiniciar a API sem mudar a imagem: sudo docker restart gh-rotas-api-api-1.
- O script não limpa imagens nem volumes automaticamente. Monitore df -h e
  sudo docker system df, principalmente no disco de 20 GiB. Retenção ECR não limpa a EC2.
  Remova imagens antigas seletivamente após validar o deploy, preservando a anterior.
- Cancelar um workflow não necessariamente cancela o comando SSM já enviado.
  Confira Run Command antes de iniciar outro; há um lock local contra concorrência.
- Rollback restaura somente a API. Migrações SQL exigem backup e plano próprio.

## Referências

- [GitHub OIDC na AWS](https://docs.github.com/en/actions/how-tos/secure-your-work/security-harden-deployments/oidc-in-aws)
- [SSM Run Command](https://docs.aws.amazon.com/systems-manager/latest/userguide/walkthrough-cli.html)
- [SSM Agent Ubuntu](https://docs.aws.amazon.com/pt_br/systems-manager/latest/userguide/agent-install-ubuntu-64-snap.html)
