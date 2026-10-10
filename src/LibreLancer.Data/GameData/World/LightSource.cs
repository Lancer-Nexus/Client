using LibreLancer.Data.Schema;

namespace LibreLancer.Data.GameData.World;

public class LightSource : NicknameItem
{
    public required string? AttenuationCurveName;
    public ColorGraph? ColorCurve;
    public float ColorCurvePeriod;
    public bool Disabled; // Editor use only, not saved.
    public RenderLight Light;

    public LightSource Clone() => new()
    {
        Nickname = Nickname,
        AttenuationCurveName = AttenuationCurveName,
        ColorCurve = ColorCurve,
        ColorCurvePeriod = ColorCurvePeriod,
        Light = Light
    };
}
