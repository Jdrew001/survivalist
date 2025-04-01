using Assets.Game.Managers;
using Assets.Game.Managers.States;
using Assets.Game.Services.Interfaces;
using Assets.Game.UI.Factory;
using UnityEngine;
using Zenject;

public class ProjectInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<IGameStateFactory>().To<GameStateFactory>().AsSingle();
        Container.Bind<ITestingService>().To<TestingService>().AsSingle();
        Container.Bind<IUIElementFactory>().To<UIElementFactory>().AsSingle();
    }
}