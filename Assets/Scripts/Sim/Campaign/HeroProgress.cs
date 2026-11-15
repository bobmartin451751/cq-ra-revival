using CQRa.Sim.Factions;
using UnityEngine;

namespace CQRa.Sim.Campaign
{
    /// <summary>Persistent campaign hero veterancy between missions.</summary>
    public static class HeroProgress
    {
        public static int Kills(FactionId faction)
        {
            return PlayerPrefs.GetInt(Key(faction), 0);
        }

        public static void Save(FactionId faction, int kills)
        {
            PlayerPrefs.SetInt(Key(faction), Mathf.Max(0, kills));
            PlayerPrefs.Save();
        }

        public static void Clear(FactionId faction)
        {
            PlayerPrefs.DeleteKey(Key(faction));
            PlayerPrefs.Save();
        }

        private static string Key(FactionId faction)
        {
            return $"cq_hero_{faction}_kills";
        }
    }
}
