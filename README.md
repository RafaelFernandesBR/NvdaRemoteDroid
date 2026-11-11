# NvdaRemoteDroid

Controle o NVDA do seu computador pelo celular Android.

## O que é

O NvdaRemoteDroid é um aplicativo Android que se conecta ao NVDA do seu computador pelo recurso de acesso remoto (NVDA Remote). Com ele, você usa o celular como um controle: envia teclas para o computador e ouve, no próprio celular, o que o NVDA está falando.

O celular só controla o computador. Ele não pode ser controlado por outra pessoa.

## O que você pode fazer

- Ouvir no celular a fala do NVDA do computador, escolhendo a voz e a velocidade que preferir.
- Usar um teclado virtual na tela, organizado em grupos: Atalhos, Navegação, Letras, Números e símbolos, Funções e Teclado numérico.
- Usar as teclas Ctrl, Alt, Shift, Win e a Tecla NVDA. Um toque aplica o modificador na próxima tecla, dois toques travam e três soltam.
- Usar atalhos prontos, como calar a fala, ler o título da janela, ler tudo, abrir o menu do NVDA e trocar de janela.
- Usar um teclado físico ou Bluetooth ligado ao celular.
- Digitar um texto no celular e enviá-lo ao computador.
- Enviar a área de transferência do celular para o computador.
- Enviar Ctrl+Alt+Del.
- Continuar conectado com o app em segundo plano. Se a conexão cair, o app tenta reconectar sozinho.
- Escolher se a Tecla NVDA será Insert ou Caps Lock.

## Requisitos

- Celular com Android 5.0 ou superior.
- Computador com o NVDA instalado e o acesso remoto (NVDA Remote) disponível.
- Internet, ou os dois aparelhos na mesma rede se você usar um servidor próprio.

## Como instalar

O app ainda não está na Play Store. A instalação é feita pelo arquivo APK:

1. Abra a [página da versão mais recente](https://github.com/RafaelFernandesBR/NvdaRemoteDroid/releases/latest) no navegador do celular.
2. Baixe o arquivo com final `.apk`.
3. Abra o arquivo baixado. Se o Android pedir, permita a instalação de apps de fontes desconhecidas para o navegador que você usou.
4. Conclua a instalação e abra o NvdaRemoteDroid.

## Como conectar

### No computador

1. No NVDA, inicie o acesso remoto como computador controlado.
2. Escolha o servidor `nvdaremote.com` (ou o seu próprio servidor).
3. Defina uma chave (uma senha para a sala). Anote essa chave.

### No celular, usando o servidor público

1. Abra a aba **Conexão**.
2. Ative **Usar servidor relay público (nvdaremote.com)**.
3. Digite a mesma chave no campo **Chave do canal**.
4. Toque em **Conectar**.

### No celular, usando conexão direta (servidor próprio)

1. Na aba **Conexão**, deixe desativada a opção do servidor público.
2. Preencha **Endereço IP ou host** e **Porta**, e a **Chave do canal**.
3. Se o seu servidor usa um certificado autoassinado, ative **Modo inseguro (certificado autoassinado)**. Na primeira conexão, o app mostra a impressão digital do certificado e pede que você confirme em **Confiar**.
4. Toque em **Conectar**.

Para remover um certificado que você já aceitou, vá em **Configurações** e abra **Certificados confiáveis**.

### O que significam as mensagens de status

- **Conectado, aguardando o computador remoto entrar no canal**: o celular conectou, mas o computador ainda não entrou com a mesma chave.
- **Computador remoto conectado**: tudo pronto, já pode controlar.
- **Chave do canal incorreta**: confira se a chave é igual nos dois lados. O app não tenta de novo sozinho nesse caso.
- **Falha de conexão, tentando novamente...**: a conexão caiu e o app está tentando reconectar.

## Usando o app

### Aba Conexão

Aqui você informa os dados do servidor e da chave, conecta e desconecta, e vê as conexões recentes e o status atual.

### Aba Controle

É a tela de uso. Ela tem o teclado virtual, o campo **Digitar texto** com o botão **Enviar texto**, e os botões **Enviar Ctrl+Alt+Del**, **Soltar todos os modificadores**, **Enviar área de transferência** e **Desconectar**.

Se você não estiver conectado, a tela avisa e oferece o botão **Ir para Conexão**.

### Aba Configurações

- **Voz da fala**: escolha o sintetizador de voz instalado no celular.
- **Velocidade da fala**: de 0,5x a 2x.
- **Testar voz**: toca uma frase de exemplo com a voz e a velocidade atuais.
- **Tecla usada como "Tecla NVDA"**: Insert ou Caps Lock.
- **Certificados confiáveis**: veja e remova certificados aceitos.
- **Compartilhar logs**: envia o registro técnico da sessão, útil para relatar um problema.

## Dicas e problemas comuns

- **Símbolos errados no computador**: os símbolos do teclado virtual seguem o teclado americano. Se o computador usa outro layout, prefira **Digitar texto**.
- **Uma tecla ficou presa** (Ctrl, Alt, Shift ou Win): use **Soltar todos os modificadores**.
- **A voz está estranha ou muda**: troque o sintetizador em **Configurações**, em **Voz da fala**, e use **Testar voz**.
- **Não conecta**: confira o endereço, a porta e se a chave é exatamente a mesma nos dois lados.

## Privacidade

A chave do canal fica salva somente no seu celular. Os logs não incluem a chave e só saem do celular se você escolher compartilhá-los.

## Encontrou um problema ou tem uma ideia?

Abra uma [issue no GitHub](https://github.com/RafaelFernandesBR/NvdaRemoteDroid/issues). Se possível, use **Compartilhar logs** em **Configurações** e anexe o arquivo.

## Para quem quer contribuir

Contribuições são bem-vindas, em issues ou pull requests. O app é feito em .NET MAUI (somente Android) e a acessibilidade é prioridade: todo controle precisa funcionar bem com leitor de tela.

Para compilar e testar (é preciso ter o workload `maui-android`, instalado com `dotnet workload install maui-android`):

```
dotnet test NvdaRemoteDroid.Tests.Unit/NvdaRemoteDroid.Tests.Unit.csproj
dotnet build -f net10.0-android
```

Detalhes de como o código está organizado estão em [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Licença

Apache License 2.0. Veja o arquivo [LICENSE](LICENSE).
