using KodiakCleaner.Core.Util;
using Xunit;

namespace KodiakCleaner.Core.Tests;

/// <summary>Sidebar Favorites (Ted, 2026-10-23): pin, unpin, move, A to Z and Restore Default Order.</summary>
public sealed class SidebarFavoritesTests
{
    private static readonly string[] Known = { "cleaner", "registry", "startup", "programs", "hash", "disk", "wiper" };

    [Fact]
    public void Never_saved_gives_Cleaner_Registry_Startup_Programs()
    {
        Assert.Equal(new[] { "cleaner", "registry", "startup", "programs" }, SidebarFavorites.Normalize(null, Known));
        Assert.Empty(SidebarFavorites.Normalize(Array.Empty<string>(), Known)); // everything unpinned stays unpinned
    }

    [Fact]
    public void Saved_list_drops_unknown_and_repeated_pages()
    {
        Assert.Equal(new[] { "hash", "cleaner" }, SidebarFavorites.Normalize(new[] { " Hash", "gone", "cleaner", "hash" }, Known));
    }

    [Fact]
    public void Pin_unpin_and_move()
    {
        var list = SidebarFavorites.Normalize(null, Known);
        Assert.True(SidebarFavorites.Pin(list, "hash"));
        Assert.False(SidebarFavorites.Pin(list, "hash"));
        Assert.True(SidebarFavorites.Pin(list, "disk", 1));
        Assert.Equal(new[] { "cleaner", "disk", "registry", "startup", "programs", "hash" }, list);
        Assert.True(SidebarFavorites.Move(list, "hash", 0));
        Assert.True(SidebarFavorites.Move(list, "cleaner", 99));
        Assert.Equal(new[] { "hash", "disk", "registry", "startup", "programs", "cleaner" }, list);
        Assert.True(SidebarFavorites.Unpin(list, "registry"));
        Assert.False(SidebarFavorites.Move(list, "registry", 0));
    }

    [Fact]
    public void Restore_default_order_puts_the_four_first_and_keeps_other_pins()
    {
        var restored = SidebarFavorites.RestoreDefaultOrder(new[] { "hash", "programs", "disk", "cleaner" }, Known);
        Assert.Equal(new[] { "cleaner", "registry", "startup", "programs", "hash", "disk" }, restored);
    }

    [Fact]
    public void A_to_Z_sorts_by_name_and_default_keeps_the_saved_order()
    {
        var names = new Dictionary<string, string> { ["cleaner"] = "Cleaner", ["registry"] = "Registry", ["hash"] = "Hash Checker", ["disk"] = "Disk Analyzer" };
        var saved = new[] { "registry", "cleaner", "hash", "disk" };
        Assert.Equal(new[] { "Registry", "Cleaner", "Hash Checker", "Disk Analyzer" }, SidebarFavorites.Arrange(saved, names, n => n, aToZ: false));
        Assert.Equal(new[] { "Cleaner", "Disk Analyzer", "Hash Checker", "Registry" }, SidebarFavorites.Arrange(saved, names, n => n, aToZ: true));
    }

    [Fact]
    public void Tools_are_A_to_Z_until_dragged_and_new_ones_go_last()
    {
        var titles = new Dictionary<string, string> { ["cleaner"] = "Cleaner", ["hash"] = "Hash Checker", ["disk"] = "Disk Analyzer", ["wiper"] = "Drive Wiper" };
        var all = titles.Keys.ToList();
        var favorites = new[] { "cleaner" };
        Assert.Equal(new[] { "disk", "wiper", "hash" }, SidebarFavorites.ArrangeTools(all, favorites, null, k => titles[k]));
        Assert.Equal(new[] { "hash", "disk", "wiper" }, SidebarFavorites.ArrangeTools(all, favorites, new[] { "hash", "cleaner", "gone" }, k => titles[k]));
        Assert.Equal(new[] { "cleaner", "disk", "hash" }, SidebarFavorites.AToZ(new[] { "hash", "cleaner", "disk" }, k => titles[k]));
    }
}
