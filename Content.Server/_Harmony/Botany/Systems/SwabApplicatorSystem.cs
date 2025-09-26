using Content.Server.Botany;
using Content.Server.Popups;
using Content.Shared.Interaction.Events;
using Robust.Shared.Containers;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Harmony.Botany.Systems;

public sealed class SwabApplicatorSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BotanySwabComponent, UseInHandEvent>(OnClean); //Harmony - Remove a swab's SeedData, used in Synthswabs
        SubscribeLocalEvent<BotanySwabComponent, ContainerGettingInsertedAttemptEvent>(OnInsertAttempt); //Harmony - Swab Applicator, on swab insert check swab has pollen, cancel if it doesn't.
        SubscribeLocalEvent<BotanySwabComponent, EntGotInsertedIntoContainerMessage>(OnInsert); //Harmony - Swab Applicator, on swab successfully inserted transfer its SeedData
        SubscribeLocalEvent<BotanySwabComponent, EntGotRemovedFromContainerMessage>(OnRemove); //Harmony - Swab Applicator, on remove swab, set Applicator's SeedData back to null
    }

    /// <summary>
    /// Harmony - Remove a swab's SeedData
    /// </summary>

    private void OnClean(Entity<BotanySwabComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!ent.Comp.Cleanable)
            return;

        ent.Comp.SeedData = null;
        _popupSystem.PopupEntity(Loc.GetString("botany-swab-clean"), ent.Owner, args.User);
        _audioSystem.PlayPvs(ent.Comp.CleanSound, ent.Owner);
        args.Handled = true;
    }

    /// <summary>
    /// Harmony - Swab Applicator, on swab insert check swab has pollen, cancel if it doesn't.
    /// </summary>
    private void OnInsertAttempt(Entity<BotanySwabComponent> ent, ref ContainerGettingInsertedAttemptEvent args)
    {
        //does the container have the botanySwab component (should always be the case)
        if (!HasComp<BotanySwabComponent>(args.Container.Owner))
            return;

        //does the swab have seeddata (aka, is not null)
        if (ent.Comp.SeedData != null)
            return;

        //if these are not true, cancel, clean swabs aren't allowed.
        _popupSystem.PopupEntity(Loc.GetString("swab-applicator-needs-pollen"), ent.Owner);
        args.Cancel();
        return;
    }

    /// <summary>
    /// Harmony - Swab Applicator, on swab successfully inserted transfer its SeedData
    /// </summary>
    private void OnInsert(Entity<BotanySwabComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (TryComp<BotanySwabComponent>(args.Container.Owner, out var applicator))
            applicator.SeedData = ent.Comp.SeedData;
    }


    /// <summary>
    /// Harmony - Swab Applicator, on remove swab, set Applicator's SeedData back to null
    /// </summary>
    private void OnRemove(Entity<BotanySwabComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (TryComp<BotanySwabComponent>(args.Container.Owner, out var applicator))
            applicator.SeedData = null;
    }
}

