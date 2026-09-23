using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

[assembly: ModInfo("SeasonRemote", "seasonRemote",
                    Authors = new string[] { "Leah" },
                    Description = "This mod enables you to pause the changing of the seasons at will.",
                    Version = "1.0.0")]

namespace SeasonRemote
{
    [ProtoContract]
    public class SeasonRemotePacket
    {
        [ProtoMember(1)]
        public bool Paused;

        [ProtoMember(2)]
        public float SeasonRel;
    }

    public class SeasonRemoteModSystem : ModSystem
    {
        bool isPaused = false;

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

            IChatCommand seasonCommandBase = api.ChatCommands.GetOrCreate("season")
                .WithDescription("See current season information or manipulate the change of the seasons.")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer();

            // Pause the season if not currently paused
            seasonCommandBase.BeginSubCommand("pause")
                .WithDescription("Pause the change of the seasons. The season will continue to be the current one.")
                .HandleWith((args) =>
                {
                    var seasonRel = (float)api.World.Calendar.YearRel;

                    var playerPos = args.Caller.Pos.AsBlockPos;
                    var seasonName = api.World.Calendar.GetSeason(playerPos);

                    if (isPaused)
                    {
                        return TextCommandResult.Success($"The seasons are already on pause. \n" +
                            $"The current season is {seasonName}");
                    }

                    api.World.Calendar.SetSeasonOverride(seasonRel);
                    isPaused = true;

                    api.Network
                        .GetChannel("seasonRemote")
                        .SendPacket(new SeasonRemotePacket { Paused = isPaused, SeasonRel = seasonRel }, api.Server.Players);

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

                    api.World.Calendar.SetSeasonOverride(null);
                    isPaused = false;

                    api.Network
                        .GetChannel("seasonRemote")
                        .SendPacket(new SeasonRemotePacket { Paused = isPaused }, api.Server.Players);

                    return TextCommandResult.Success("The seasons have resumed changing!");
                })
                .EndSubCommand();

            // Show current season and whether it is paused
            seasonCommandBase.BeginSubCommand("info")
                .WithDescription("Get information about the current season and whether the seasons are changing or on pause.")
                .HandleWith((args) =>
                {
                    var seasonRel = (float)api.World.Calendar.YearRel;
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
}
