using Emberfall.Data;
using UnityEditor;
using UnityEngine;

namespace Emberfall.Editor
{
    /// <summary>
    /// Persists the code-defined starter content as ScriptableObject assets so
    /// designers can edit them. Ids are stable; re-running overwrites values but
    /// keeps the same asset paths (and therefore the same .meta GUIDs).
    /// </summary>
    public static class StarterContentMenu
    {
        private const string Root = "Assets/_Project/Content";

        [MenuItem("Emberfall/Create Starter Content")]
        public static void Create()
        {
            EnsureFolder("Assets/_Project", "Content");
            EnsureFolder(Root, "Weapons");
            EnsureFolder(Root, "Armour");
            EnsureFolder(Root, "Enemies");
            EnsureFolder(Root, "Skills");
            EnsureFolder(Root, "Quests");

            var runtime = StarterContent.CreateRuntimeDatabase();

            var db = LoadOrCreate<ItemDatabase>(Root + "/ItemDatabase.asset");
            db.Weapons.Clear(); db.Armour.Clear(); db.Enemies.Clear(); db.Skills.Clear(); db.Quests.Clear();

            foreach (var w in runtime.Weapons) db.Weapons.Add(Persist(w, Root + "/Weapons/" + w.Id + ".asset"));
            foreach (var a in runtime.Armour) db.Armour.Add(Persist(a, Root + "/Armour/" + a.Id + ".asset"));
            foreach (var e in runtime.Enemies) db.Enemies.Add(Persist(e, Root + "/Enemies/" + e.Id + ".asset"));
            foreach (var s in runtime.Skills) db.Skills.Add(Persist(s, Root + "/Skills/" + s.Id + ".asset"));
            foreach (var q in runtime.Quests) db.Quests.Add(Persist(q, Root + "/Quests/" + q.Id + ".asset"));

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Object.DestroyImmediate(runtime);
            Debug.Log("Emberfall starter content created under " + Root +
                      ". Assign ItemDatabase to GrayboxBootstrap and press Play.");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static T Persist<T>(T source, string path) where T : ScriptableObject
        {
            var target = LoadOrCreate<T>(path);
            if (target != source) EditorUtility.CopySerialized(source, target);
            EditorUtility.SetDirty(target);
            Object.DestroyImmediate(source);
            return target;
        }
    }
}
