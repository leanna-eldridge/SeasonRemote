using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

[assembly: ModInfo(
    "Season Remote",
    "seasonremote",
    Authors = new[] { "ihaskittykat" },
    Description = "This mod enables you to pause the changing of the seasons at will.",
    Version = "1.0.1"
)]

namespace SeasonRemote;

[ProtoContract]
public class SeasonRemotePacket
{
    [ProtoMember(1)]
    public bool Paused;

    [ProtoMember(2)]
    public float? SeasonRel;
}

public class SeasonRemoteModSystem : ModSystem
{
    private const string PausedKey = "seasonremote:paused";
    private const string SeasonRelKey = "seasonremote:seasonrel";

    private bool isPaused;
    private float? pausedSeasonRel;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        api.Network
            .RegisterChannel("seasonRemote")
            .RegisterMessageType<SeasonRemotePacket>();
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        api.Event.SaveGameLoaded += () =>
        {
            isPaused = api.World.Config.GetBool(PausedKey, false);
            pausedSeasonRel = api.World.Config.GetFloat(SeasonRelKey, 0f);

            if (isPaused)
            {
                api.World.Calendar.SetSeasonOverride(pausedSeasonRel);
            }
        };

        api.Event.PlayerJoin += (player) =>
        {
            SendSeasonState([player]);
        };

        IChatCommand seasonCommandBase = api.ChatCommands.GetOrCreate("season")
            .WithDescription("See current season information or manipulate the change of the seasons.")
            .RequiresPrivilege(Privilege.chat)
            .RequiresPlayer();

        // Pause the season if not currently paused
        seasonCommandBase.BeginSubCommand("pause")
            .WithDescription("Pause the change of the seasons. The season will continue to be the current one.")
            .HandleWith((args) =>
            {
                var playerPos = args.Caller.Pos.AsBlockPos;
                var seasonName = api.World.Calendar.GetSeason(playerPos);

                if (isPaused)
                {
                    return TextCommandResult.Success($"The seasons are already on pause. \n" +
                        $"The current season is {seasonName}");
                }

                SetSeasonState(paused: true, seasonRel: (float)api.World.Calendar.YearRel);
                SendSeasonState(api.Server.Players);
                SaveSeasonState();

                return TextCommandResult.Success($"The seasons have been paused. It will remain {seasonName}.");
            })
            .EndSubCommand();

        // Resume the season if currently paused
        seasonCommandBase.BeginSubCommand("resume")
            .WithDescription("Resume the change of the seasons. The season will become the one typically associated with the current date.")
            .HandleWith((args) =>
            {
                if (!isPaused)
                {
                    return TextCommandResult.Success("The seasons are already changing!");
                }

                SetSeasonState(paused: false, seasonRel: null);
                SendSeasonState(api.Server.Players);
                SaveSeasonState();

                return TextCommandResult.Success("The seasons have resumed changing!");
            })
            .EndSubCommand();

        // Show current season and whether it is paused
        seasonCommandBase.BeginSubCommand("info")
            .WithDescription("Get information about the current season and whether the seasons are changing or on pause.")
            .HandleWith((args) =>
            {
                var message = string.Empty;
                var playerPos = args.Caller.Pos.AsBlockPos;
                var seasonName = api.World.Calendar.GetSeason(playerPos);

                if (isPaused)
                {
                    message = $"The seasons are on pause. \n" +
                    $"The current season is {seasonName}";
                }
                else
                {
                    message = $"The seasons are changing! \n" +
                    $"The current season is {seasonName}";
                }

                return TextCommandResult.Success(message);
            })
            .EndSubCommand();

        void SetSeasonState(bool paused, float? seasonRel)
        {
            isPaused = paused;
            pausedSeasonRel = seasonRel;
            api.World.Calendar.SetSeasonOverride(pausedSeasonRel);
        }

        void SendSeasonState(IServerPlayer[] players)
        {
            api.Network
                    .GetChannel("seasonRemote")
                    .SendPacket(new SeasonRemotePacket { Paused = isPaused, SeasonRel = pausedSeasonRel }, players);
        }

        void SaveSeasonState()
        {
            api.World.Config.SetBool(PausedKey, isPaused);
            api.World.Config.SetFloat(SeasonRelKey, pausedSeasonRel ?? 0f);
        }
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);

        api.Network
            .GetChannel("seasonRemote")
            .SetMessageHandler<SeasonRemotePacket>(packet =>
            {
                api.World.Calendar.SetSeasonOverride(packet.Paused ? packet.SeasonRel : null);
            });
    }
}
