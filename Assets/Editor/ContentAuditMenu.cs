using CQRa.Sim.Content;
using UnityEditor;
using UnityEngine;

namespace CQRa.EditorTools
{
    /// <summary>Editor menu entry for the shared content audit.</summary>
    public static class ContentAuditMenu
    {
        [MenuItem("CQRA/Validate Content")]
        public static void ValidateContent()
        {
            var issues = ContentAudit.ValidateAll();
            var report = ContentAudit.Format(issues);
            if (issues.Count == 0)
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogWarning(report);
            }

            EditorUtility.DisplayDialog("CQRA Content Audit", report, "OK");
        }
    }
}
