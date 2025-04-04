using Assets.Game.Managers.States;
using UnityEngine;
using Zenject;

public class GameSceneInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        //Container.Bind<TerrainGenerator>().AsSingle();
        //Container.Bind<CombinedNoiseGenerator>().AsSingle();
    }
}