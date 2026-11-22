// Headless test shim: minimal stand-ins for the two Unity APIs the pure core
// touches (JsonUtility, Application.persistentDataPath). Used ONLY by
// tools/headless to compile Core + EditMode tests with mono on machines
// without the Unity Editor. Never ships in the game.
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace UnityEngine
{
    public static class Application
    {
        public static string persistentDataPath =
            Path.Combine(Path.GetTempPath(), "emberfall_headless_data");
    }

    public static class JsonUtility
    {
        // JavaScriptSerializer walks public fields like JsonUtility does and
        // writes enums as ints. Close enough for save round-trip tests.
        private static JavaScriptSerializer NewSerializer()
        {
            var s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            return s;
        }

        public static string ToJson(object obj, bool pretty)
        {
            return NewSerializer().Serialize(obj);
        }

        public static T FromJson<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return default(T);
            return NewSerializer().Deserialize<T>(json);
        }
    }
}
