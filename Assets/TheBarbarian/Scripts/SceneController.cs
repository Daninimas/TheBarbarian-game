using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using TheBarbarian.Scripts.Tools;

/// <summary>
/// Manages game scenes transitions.
/// </summary>

namespace TheBarbarian.Scripts
{
    public class SceneController
    {
        public enum SceneName
        {
            MainMenu,
            ActionPhase,
            Intermediate
        }

        public SceneName CurrentScene
        {
            get;
            private set;
        }

        public SceneName? PrevScene
        {
            get;
            private set;
        }
        
        public bool CanLoadNextScene => _canLoadNextScene;
        private bool _canLoadNextScene = false;
        public bool CanChangeScene => _canChangeScene;
        private bool _canChangeScene = true;

        public bool IntermediateSceneLoaded => _intermediateSceneLoaded;

        private bool _intermediateSceneLoaded = false;

        public event Action<SceneName> OnSceneUnloaded;
        public event Action<SceneName> BeforeSceneLoad;
        public event Action<SceneName> OnSceneLoaded;


        #region Constructor
        
        public SceneController()
        {
            CurrentScene = SceneName.MainMenu;
        }
        
        #endregion
        

        #region Event subscription
        
        private void SubscribeEvents()
        {
            UnsubscribeEvents();
            
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += InvokeOnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += InvokeOnSceneUnloaded;
        }
        
        private void UnsubscribeEvents()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= InvokeOnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= InvokeOnSceneUnloaded;
        }

        #endregion
        

        private void InvokeOnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            Log.Info("SceneController", $"Scene, loaded {scene.name}");
            if (scene.name != "Intermediate")
            {
                OnSceneLoaded?.Invoke((SceneName)scene.buildIndex);
            }
            else
            {
                _intermediateSceneLoaded = true;
            }
        }

        private void InvokeOnSceneUnloaded(Scene scene)
        {
            Log.Info("SceneController", $"Scene, Unloaded {scene.name}");
            if (scene.name != "Intermediate")
            {
                OnSceneUnloaded?.Invoke((SceneName)scene.buildIndex);
            }
        }

        public void LoadScene(SceneName scene, float waitTime = 0, bool forceLoad = true)
        {
            if (forceLoad == false && UnityEngine.SceneManagement.SceneManager.GetSceneByBuildIndex((int)scene).isLoaded) return;
            
            PrevScene = CurrentScene;
            
            LoadSceneAsync(scene, waitTime, LoadSceneMode.Single).Forget();
        }

        public void ReloadScene(float waitTime = 0)
        {
            LoadSceneAsync(CurrentScene, waitTime, LoadSceneMode.Single).Forget();
        }

        private async UniTaskVoid LoadSceneAsync(SceneName scene, float waitTime, LoadSceneMode mode)
        {
            if (waitTime > 0)
                await UniTask.WaitForSeconds(waitTime);
            
            _canChangeScene = false;
            BeforeSceneLoad?.Invoke(scene);
            
            _intermediateSceneLoaded = false;
            
            await SceneManager.LoadSceneAsync((int)SceneName.Intermediate, LoadSceneMode.Single);
            _intermediateSceneLoaded = true;
            
            await SceneManager.LoadSceneAsync((int)scene, mode);
            _intermediateSceneLoaded = false;

            CurrentScene = scene;
            _canChangeScene = true;
            _canLoadNextScene = false;
        }
    }
}