using Robust.Shared.GameStates;

namespace Content.Shared._Harmony.SignalJammer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SignalJammerComponent : Component
{
    [DataDefinition]
    public partial struct Setting
    {
        [DataField(required: true)]
        public float Wattage;

        [DataField(required: true)]
        public float CameraRange;

        [DataField(required: true)]
        public float AiRange;

        [DataField(required: true)]
        public float DoorRange;

        [DataField(required: true)]
        public float RemoteRange;

        [DataField(required: true)]
        public LocId Name = string.Empty;

        [DataField(required: true)]
        public LocId Message = string.Empty;
    }

    [DataField(required: true)]
    public Setting[] Settings = [];

    [DataField, AutoNetworkedField]
    public int SelectedPowerLevel = 1;
}

public enum SignalJammerEffect : byte
{
    Camera,
    AiVision,
    DoorControl,
    DoorRemote,
}
