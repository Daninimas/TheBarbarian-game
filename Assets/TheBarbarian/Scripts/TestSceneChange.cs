using TheBarbarian.Scripts.Managers;
using TheBarbarian.Scripts.Managers.SceneController;
using UnityEngine;
using VContainer;

namespace TheBarbarian.Scripts
{
    public class TestSceneChange : MonoBehaviour
    {
        private SceneController _sceneController;
        
        [Inject]
        private void SetUp(SceneController sceneController)
        {
            _sceneController = sceneController;
        }

        public void LoadActionPhase()
        {
            _sceneController.LoadScene(SceneController.SceneName.ActionPhase);
        }
        
        public void LoadMainMenu()
        {
            _sceneController.LoadScene(SceneController.SceneName.MainMenu);
        }
    }
}