using UnityEngine;

namespace LastStand.Controls
{
    public enum CursorKind
    {
        Default = 0,
        Attack = 1,
        Target = 2
    }

    /// <summary>Курсоры в стиле Art Deco. Меняются только при смене типа, чтобы не дёргать ОС каждый кадр.</summary>
    [CreateAssetMenu(menuName = "Last Stand/Cursor Set", fileName = "CursorSet")]
    public sealed class CursorSet : ScriptableObject
    {
        public Texture2D defaultCursor;
        public Vector2 defaultHotspot = new Vector2(3f, 3f);
        public Texture2D attackCursor;
        public Vector2 attackHotspot = new Vector2(3f, 3f);
        public Texture2D targetCursor;
        public Vector2 targetHotspot = new Vector2(24f, 24f);

        [System.NonSerialized] private CursorKind current = (CursorKind)(-1);

        public void Apply(CursorKind kind)
        {
            if (kind == current)
                return;
            current = kind;
            switch (kind)
            {
                case CursorKind.Attack:
                    Cursor.SetCursor(attackCursor, attackHotspot, CursorMode.Auto);
                    break;
                case CursorKind.Target:
                    Cursor.SetCursor(targetCursor, targetHotspot, CursorMode.Auto);
                    break;
                default:
                    Cursor.SetCursor(defaultCursor, defaultHotspot, CursorMode.Auto);
                    break;
            }
        }

        public void ResetCursor()
        {
            current = (CursorKind)(-1);
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
