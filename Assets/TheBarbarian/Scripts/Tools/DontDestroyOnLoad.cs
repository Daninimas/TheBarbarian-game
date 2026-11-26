using UnityEngine;

namespace TheBarbarian.Scripts.Tools
{
    public class DontDestroyOnLoad : MonoBehaviour
    {
        public void OnEnable()
        {
            DontDestroyOnLoad(this);
        }   
    }
}