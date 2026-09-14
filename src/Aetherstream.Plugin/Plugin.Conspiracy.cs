using Aetherstream.Plugin.Video;

namespace Aetherstream.Plugin;

/// <summary>
/// What the conspiracy show has to work with: real things out of the game's tables, a vista, a
/// creature, or an item, each with a picture, for the host to explain was the Allagans.
/// </summary>
public sealed partial class Plugin
{
    private List<ConspiracySubject>? conspiracySubjects;

    private IReadOnlyList<ConspiracySubject> ConspiracySubjects()
    {
        if (this.conspiracySubjects is not null)
            return this.conspiracySubjects;

        var list = new List<ConspiracySubject>();
        foreach (var v in this.Vistas())
            list.Add(new ConspiracySubject(SubjectKind.Place, v.Name, v.Place, v.Description, v.Icon, ItemPicture: false));
        foreach (var c in this.Creatures())
        {
            if (c.Icon != 0)
                list.Add(new ConspiracySubject(SubjectKind.Creature, c.Name, c.Zone, string.Empty, c.Icon, ItemPicture: false));
        }

        // A slice of the items, so the show is not all crafting materials: every seventh marketable one.
        var items = this.AdItems();
        for (var i = 0; i < items.Count; i += 7)
            list.Add(new ConspiracySubject(SubjectKind.Thing, items[i].Name, items[i].Category, string.Empty, items[i].Id, ItemPicture: true));

        this.log.Information($"[conspiracy] {list.Count} subjects");
        this.conspiracySubjects = list;
        return list;
    }

    /// <summary>A subject's picture: an item's icon by item id, anything else by icon id.</summary>
    private IconPixels? SubjectPicture(ConspiracySubject subject) =>
        subject.ItemPicture ? this.ItemIcon(subject.Picture) : this.Icon(subject.Picture);
}
