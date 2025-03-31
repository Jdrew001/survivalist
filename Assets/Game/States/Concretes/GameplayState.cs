using UnityEngine;

namespace Assets.Game.Managers
{
    public class GameplayState : GameState
    {
        public GameplayState(GameStateManager manager) : base(manager) { }

        public override void Enter()
        {
            Debug.Log("Entered GAMEPLAY State");
            // Enable player controls, hide main menu UI
            Time.timeScale = 1f; // Ensure normal timescale if unpausing
        }

        public override void Update()
        {
            // Check for pause input
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                manager.SetState(new PauseState(manager));
            }

            // Your normal gameplay logic
        }

        public override void Exit()
        {
            Debug.Log("Exiting GAMEPLAY State");
            // Cleanup if needed
        }
    }
}
