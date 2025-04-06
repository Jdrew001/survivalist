using Assets.Game.Managers.States;
using Assets.Game.Services;
using Assets.Game.Services.Interfaces;
using EndlessTerrain;
using UnityEngine;
using Zenject;

public class GameSceneInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<TerrainManager>().AsSingle();
        Container.Bind<IPlayerService>().To<PlayerService>().AsSingle();
    }
}