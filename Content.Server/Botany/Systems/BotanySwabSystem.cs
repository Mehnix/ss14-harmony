using Content.Server.Botany.Components;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Swab;
using Content.Shared.Interaction.Events; //Harmony
using Robust.Shared.Containers;          //
using Robust.Shared.Audio.Systems;       //Harmony

namespace Content.Server.Botany.Systems;

public sealed class BotanySwabSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly MutationSystem _mutationSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!; //Harmony

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BotanySwabComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BotanySwabComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<BotanySwabComponent, BotanySwabDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<BotanySwabComponent, UseInHandEvent>(OnClean); //Harmony - Remove a swab's SeedData
        SubscribeLocalEvent<BotanySwabComponent, ContainerGettingInsertedAttemptEvent>(OnInsertAttempt); //Harmony - Swab Applicator, on swab insert check swab has pollen, cancel if it doesn't.
        SubscribeLocalEvent<BotanySwabComponent, EntGotInsertedIntoContainerMessage>(OnInsert); //Harmony - Swab Applicator, on swab successfully inserted transfer its SeedData
        SubscribeLocalEvent<BotanySwabComponent, EntGotRemovedFromContainerMessage>(OnRemove); //Harmony - Swab Applicator, on remove swab, set Applicator's SeedData back to null
    }

    /// <summary>
    /// This handles swab examination text
    /// so you can tell if they are used or not.
    /// </summary>
    private void OnExamined(EntityUid uid, BotanySwabComponent swab, ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
        {
            if (swab.SeedData != null)
                args.PushMarkup(Loc.GetString("swab-used"));
            else if (swab.UsableIfClean == true) // Harmony, hides unused text description on swab applicator
                args.PushMarkup(Loc.GetString("swab-unused"));
        }
    }

    /// <summary>
    /// Handles swabbing a plant.
    /// </summary>
    private void OnAfterInteract(EntityUid uid, BotanySwabComponent swab, AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach || !HasComp<PlantHolderComponent>(args.Target))
            return;

        if (swab.UsableIfClean == false && swab.SeedData == null) //Harmony, Swab Applicator, prevents applicator use if no swab inside
        {
            _popupSystem.PopupEntity(Loc.GetString("botany-swab-unusable"), uid, args.User);
            return;
        }

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, swab.SwabDelay, new BotanySwabDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            Broadcast = true,
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    /// <summary>
    /// Save seed data or cross-pollenate.
    /// </summary>
    private void OnDoAfter(EntityUid uid, BotanySwabComponent swab, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !TryComp<PlantHolderComponent>(args.Args.Target, out var plant))
            return;

        _audioSystem.PlayPvs(swab.SwabSound, uid); //Harmony, swab sound

        if (swab.SeedData == null)
        {
            // Pick up pollen
            swab.SeedData = plant.Seed;
            _popupSystem.PopupEntity(Loc.GetString("botany-swab-from"), args.Args.Target.Value, args.Args.User);
        }
        else
        {
            var old = plant.Seed;
            if (old == null)
                return;

            plant.Seed = _mutationSystem.Cross(swab.SeedData, old); // Cross-pollenate

            if (swab.Contaminate) // Harmony, Transfer old plant pollen to swab only if contamination is allowed
                swab.SeedData = old;

            _popupSystem.PopupEntity(Loc.GetString("botany-swab-to"), args.Args.Target.Value, args.Args.User);
        }
        args.Handled = true;
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

