using System.Numerics;

namespace KitsuneEngine.UI;

public class UISystem
{
    public DialogueManager? Dialogue { get; private set; }

    public Panel Root { get; } = new Panel
    {
        Name = "Root",
        Layout = PanelLayout.Free
    };

    public void EnableDialogue(DialogueSettings? settings = null)
    {
        Dialogue = new DialogueManager(settings);
    }

    public void DisableDialogue()
    {
        Dialogue?.Stop();
        Dialogue = null;
    }

    public void Update(float deltaTime)
    {
        Dialogue?.Update(deltaTime);
        Root.Update(deltaTime);
    }

    public void Render(UIRenderer renderer)
    {
        renderer.Render(Root);
    }

    public bool HandlePointerMove(Vector2 pointer)
    {
        bool anyHover = false;
        Traverse(Root, element =>
        {
            if (element is Button button)
            {
                bool hovered = button.ContainsPoint(pointer);
                button.SetHovered(hovered);
                anyHover |= hovered;
            }
        });

        return anyHover;
    }

    public bool HandlePointerDown(Vector2 pointer)
    {
        var button = FindTopMostButtonAt(pointer);
        if (button == null)
            return false;

        button.SetPressed(true);
        return true;
    }

    public bool HandlePointerUp(Vector2 pointer)
    {
        var button = FindTopMostButtonAt(pointer);
        bool clicked = false;

        Traverse(Root, element =>
        {
            if (element is Button b)
            {
                if (b.IsPressed)
                {
                    b.SetPressed(false);
                    if (ReferenceEquals(b, button))
                    {
                        b.Click();
                        clicked = true;
                    }
                }
            }
        });

        return clicked;
    }

    private Button? FindTopMostButtonAt(Vector2 pointer)
    {
        Button? result = null;
        int maxZ = int.MinValue;

        Traverse(Root, element =>
        {
            if (element is Button button && button.Visible && button.Enabled && button.ContainsPoint(pointer))
            {
                if (button.ZIndex >= maxZ)
                {
                    maxZ = button.ZIndex;
                    result = button;
                }
            }
        });

        return result;
    }

    private static void Traverse(UIElement element, Action<UIElement> action)
    {
        action(element);
        foreach (var child in element.Children)
        {
            Traverse(child, action);
        }
    }
}
