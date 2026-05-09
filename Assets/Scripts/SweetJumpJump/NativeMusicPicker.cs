using System.Runtime.InteropServices;
using UnityEngine;

namespace SweetJumpJump
{
    public static class NativeMusicPicker
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void SJJ_OpenMusicPicker(string gameObjectName);
#endif

        public static bool IsSupported
        {
            get { return Application.platform == RuntimePlatform.IPhonePlayer; }
        }

        public static void Open(string gameObjectName)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SJJ_OpenMusicPicker(gameObjectName);
#else
            Debug.Log("Native music picker is only available on iOS devices.");
#endif
        }
    }
}
