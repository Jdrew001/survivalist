using Assets.Game.Managers.States;
using Assets.Game.Systems.TerrainSystem;
using Assets.Game.Systems.TerrainSystem.Generators;
using UnityEngine;
using Zenject;

public class GameSceneInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<TerrainGenerator>().AsSingle();
        Container.Bind<CombinedNoiseGenerator>().AsSingle();
    }
}