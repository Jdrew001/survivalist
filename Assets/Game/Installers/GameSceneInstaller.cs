using Assets.Game.Managers.States;
using EndlessTerrain;
using UnityEngine;
using Zenject;

public class GameSceneInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<TerrainManager>().AsSingle();
    }
}