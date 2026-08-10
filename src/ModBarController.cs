using UnityEngine;

namespace NoModBar
{
    internal class ModBarController : MonoBehaviour
    {
        private ModBarCanvas _bar;
        private int _lastVersion = -1;

        private void Awake()
        {
            _bar = new ModBarCanvas();
            _bar.Create();
        }

        private void Update()
        {
            bool inGame = !Plugin.RequireInGame.Value || IsInGame();
            if (inGame != _bar.Visible)
                _bar.SetVisible(inGame);

            _bar.ApplyOffsets();

            if (ModBarApi.Version != _lastVersion)
            {
                _lastVersion = ModBarApi.Version;
                _bar.Rebuild(ModBarApi.Snapshot());
            }

            if (inGame)
                _bar.RefreshActiveStates();
        }

        internal static bool IsInGame()
        {
            try
            {
                Aircraft aircraft;
                return GameManager.GetLocalAircraft(out aircraft);
            }
            catch
            {
                return false;
            }
        }

        private void OnDestroy()
        {
            if (_bar != null) _bar.Destroy();
        }
    }
}
