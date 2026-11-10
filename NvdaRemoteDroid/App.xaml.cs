using NvdaRemoteDroid.Core.Session;

namespace NvdaRemoteDroid;

public partial class App : Application
{
	// Recebidos só para serem instanciados junto com o app: ambos assinam eventos do transporte
	// no construtor e precisam existir antes da primeira conexão, não quando alguma tela pedir.
	public App(IRemoteSessionCoordinator sessionCoordinator, ForegroundConnectionController foregroundConnectionController)
	{
		InitializeComponent();
		_ = sessionCoordinator;
		_ = foregroundConnectionController;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}
