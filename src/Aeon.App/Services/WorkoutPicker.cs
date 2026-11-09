using Aeon.App.Models;
using Aeon.App.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Aeon.App.Services;

/// <summary>
/// The "add this record to a workout" flow, shared by ComplexDetailPage and
/// ProtocolDetailPage so both behave identically: pick one of the existing workouts
/// (numbered, so two workouts with the same name stay unambiguous), create a new one
/// inline, then add the entry and offer to jump straight to the workout.
/// The store is resolved from the app services at the moment of the tap — detail
/// pages are constructed with `new` deep inside the browse stack, and threading a
/// dependency through Sports → Roles → Categories → Complexes (and the conditioning
/// chain) just to reach one service would touch eight files for no gain.
/// </summary>
public static class WorkoutPicker
{
    public static async Task AddAsync(ContentPage host, BundleService bundle, WorkoutEntry entry)
    {
        var store = Application.Current?.Handler?.MauiContext?.Services.GetService<WorkoutStore>()
            ?? throw new InvalidOperationException("WorkoutStore is not registered");

        var ordered = store.Workouts.OrderByDescending(w => w.UpdatedUtc).ToList();
        Workout? target;

        if (ordered.Count == 0)
        {
            target = await CreateAsync(host, store);
            if (target is null)
            {
                return;
            }
        }
        else
        {
            var options = new List<string>(ordered.Count + 1);
            for (var i = 0; i < ordered.Count; i++)
            {
                options.Add($"{i + 1}. {ordered[i].Name}");
            }
            options.Add("New workout…");

            var choice = await host.DisplayActionSheetAsync("Add to workout", "Cancel", null, options.ToArray());
            if (choice is null || choice == "Cancel")
            {
                return;
            }
            if (choice == "New workout…")
            {
                target = await CreateAsync(host, store);
                if (target is null)
                {
                    return;
                }
            }
            else
            {
                var index = int.Parse(choice.Split('.')[0]) - 1;
                target = ordered[index];
            }
        }

        await store.AddEntryAsync(target, entry);

        var open = await host.DisplayAlertAsync(
            "Added", $"Added \"{entry.NameSnapshot}\" to \"{target.Name}\".", "Open workout", "OK");
        if (open)
        {
            await host.Navigation.PushAsync(new WorkoutDetailPage(store, bundle, target));
        }
    }

    private static async Task<Workout?> CreateAsync(ContentPage host, WorkoutStore store)
    {
        var name = await host.DisplayPromptAsync("New workout", "Name (e.g. Tuesday Power)", "Create", "Cancel");
        return string.IsNullOrWhiteSpace(name) ? null : await store.CreateAsync(name, "");
    }
}