
namespace Assets.Game.Managers.States
{
    public interface IGameStateFactory
    {
        StartState CreateStartState(GameStateManager manager);
        GameState CreateGameState(GameStateManager manager);
        PauseState CreatePauseState(GameStateManager manager);
        DeathState CreateDeathState(GameStateManager manager);
    }
}
