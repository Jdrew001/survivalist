using UnityEngine;

namespace Assets.Game.Managers
{
    public class PauseState : GameState
    {
        public PauseState(GameStateManager manager) : base(manager) { }

        public override void Enter()
        {
            Debug.Log("Entered PAUSE State");
            // Show pause menu
            Time.timeScale = 0f; // Freeze game logic (except UI)
        }

        public override void Update()
        {
            // Check if user unpauses
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                manager.SetState(new GameplayState(manager));
            }
        }

        public override void Exit()
        {
            Debug.Log("Exiting PAUSE State");
            // Hide pause menu
            Time.timeScale = 1f;
        }
    }
}
