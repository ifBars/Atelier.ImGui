namespace Atelier;

/// <summary>A menu layout that can be rendered by an existing host or the optional standalone adapter.</summary>
public interface IMenuSurface
{
    bool IsOpen { get; }
    event Action? DragStarted;
    void Draw(bool fillViewport = false);
}
