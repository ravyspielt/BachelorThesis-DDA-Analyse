using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class UiNavigation
{
    public static void ConfigureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return;
        }

        eventSystem.sendNavigationEvents = true;

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule != null)
        {
            inputModule.deselectOnBackgroundClick = false;
        }
    }

    public static void Select(GameObject target)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || target == null || !target.activeInHierarchy)
        {
            return;
        }

        if (eventSystem.currentSelectedGameObject != target)
        {
            eventSystem.SetSelectedGameObject(null);
        }

        eventSystem.SetSelectedGameObject(target);
    }

    public static void KeepSelection(GameObject fallback)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || fallback == null || !fallback.activeInHierarchy)
        {
            return;
        }

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected == null || !selected.activeInHierarchy)
        {
            Select(fallback);
        }
    }

    public static void StyleButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.88f, 1f, 0.45f, 1f);
        colors.selectedColor = new Color(1f, 0.82f, 0.15f, 1f);
        colors.pressedColor = new Color(0.72f, 0.82f, 0.12f, 1f);
        colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    public static void SetExplicitNavigation(Selectable selectable, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        if (selectable == null)
        {
            return;
        }

        Navigation navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down,
            selectOnLeft = left,
            selectOnRight = right
        };
        selectable.navigation = navigation;
    }

    public static void EnsureVisible(ScrollRect scrollRect, RectTransform target)
    {
        if (scrollRect == null || target == null || scrollRect.content == null)
        {
            return;
        }

        RectTransform viewport = scrollRect.viewport != null
            ? scrollRect.viewport
            : scrollRect.GetComponent<RectTransform>();

        if (viewport == null)
        {
            return;
        }

        Vector3[] itemCorners = new Vector3[4];
        Vector3[] viewCorners = new Vector3[4];
        target.GetWorldCorners(itemCorners);
        viewport.GetWorldCorners(viewCorners);

        float worldDelta = 0f;
        if (itemCorners[1].y > viewCorners[1].y)
        {
            worldDelta = itemCorners[1].y - viewCorners[1].y;
        }
        else if (itemCorners[0].y < viewCorners[0].y)
        {
            worldDelta = itemCorners[0].y - viewCorners[0].y;
        }

        if (Mathf.Abs(worldDelta) < 0.01f)
        {
            return;
        }

        Vector3 localDelta = scrollRect.content.InverseTransformVector(new Vector3(0f, worldDelta, 0f));
        scrollRect.content.anchoredPosition -= new Vector2(0f, localDelta.y);
    }
}
