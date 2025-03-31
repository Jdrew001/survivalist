using Assets.Game.Managers.States;
using Assets.Game.Services.Interfaces;
using UnityEngine;
using Zenject;

public class ProjectInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Debug.Log("INSTALL BINDINGS CALLED");
        Container.Bind<IGameStateFactory>().To<GameStateFactory>().AsSingle();
        Container.Bind<ITestingService>().To<TestingService>().AsSingle();
    }
}