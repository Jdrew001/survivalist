using Assets.Game.Managers;
using UnityEngine;
using Zenject;

public class SceneInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<GameStateManager>()
            .FromComponentInHierarchy()
            .AsSingle();
    }
}