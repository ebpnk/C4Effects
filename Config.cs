using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Config;
using System.Text.Json.Serialization;

namespace C4Effects;

public class C4EffectsConfig : BasePluginConfig
{
    [JsonPropertyName("ParticleList")]
    public Dictionary<string, string> ParticleList { get; set; } = new()
    {
        { "status_level_2", "particles/ui/status_levels/ui_status_level_2.vpcf" },
        { "status_level_1_right", "particles/ui/status_levels/ui_status_level_1_a_right.vpcf" },
        { "status_level_4_energy", "particles/ui/status_levels/ui_status_level_4_energycirc.vpcf" },
        { "status_level_8", "particles/ui/status_levels/ui_status_level_8.vpcf" },
        { "blood_pool", "particles/blood_impact/blood_pool.vpcf" },
        { "gas_ring_embers", "particles/burning_fx/gas_cannister_idle_ring_embers.vpcf" },
        { "dev_color_preview", "particles/dev/dev_preview_sequence_color_effect.vpcf" },
        { "smoke_grenade_smoke", "particles/explosions_fx/explosion_smokegrenade_s1_child_smoke_bottom.vpcf" },
        { "steam_fire", "particles/maps/generic/steam_fire_test.vpcf" },
        { "test_freeze", "particles/testsystems/test_endcap_freeze.vpcf" },
        { "carepackage_receive", "particles/ui/rank_carepackage_recieve.vpcf" },
        { "ui_electric", "particles/ui/ui_electric_gold.vpcf" },
        { "xp_award_center", "particles/ui/ui_experience_award_innerpoint.vpcf" },
        { "bomb_plant_ping", "particles/ui/hud/ui_map_bomb_plant_ping.vpcf" },
        { "water_splash", "particles/water_impact/water_splash_02_continuous.vpcf" },
        { "movie_fog", "particles/ambient_fx/cbbl_movie_fog.vpcf" },
        { "gas_ring_embers2", "particles/burning_fx/gas_cannister_idle_ring_embers.vpcf" },
        { "beacon_smoke", "particles/explosions_fx/beacon_smoke.vpcf" },
        { "firework_ground", "particles/inferno_fx/firework_crate_ground_effect_fallback1.vpcf" },
        { "office_embers", "particles/maps/cs_office/office_child_embers01a.vpcf" },
        { "dust_devil", "particles/maps/de_dust/dust_devil_smoke.vpcf" },
        { "rain_puddle", "particles/rain_fx/rain_puddle_ripples_large.vpcf" },
        { "rain_drops", "particles/rain_fx/rain_single_800.vpcf" },
        { "feather_test", "particles/testsystems/feathertest.vpcf" },
        { "ui_star_glow", "particles/ui/ui_element_horiz_star_glow.vpcf" },
        { "xp_rolling_rings", "particles/ui/ui_experience_award_rollingrings.vpcf" },
        { "xp_rolling_inner", "particles/ui/ui_experience_award_rollingrings_inner.vpcf" },
        { "hud_transition", "particles/ui/hud/hud_mainwin_panel_transition.vpcf" },
        { "status_lightning", "particles/ui/status_levels/ui_status_level7_lightning.vpcf" },
        { "status_level_1", "particles/ui/status_levels/ui_status_level_1.vpcf" },
        { "status_level_3d", "particles/ui/status_levels/ui_status_level_3d.vpcf" },
        { "status_level_7_energy", "particles/ui/status_levels/ui_status_level_7_energycirc.vpcf" },
        { "status_level_8_energy", "particles/ui/status_levels/ui_status_level_8_energycirc.vpcf" }
    };

    [JsonPropertyName("SelectedEffect")]
    public string SelectedEffect { get; set; } = "blood_pool"; // Глобальный эффект по умолчанию (для игроков без персонального)

    [JsonPropertyName("Scale")]
    public float Scale { get; set; } = 1.0f;

    [JsonPropertyName("AlphaScale")]
    public float AlphaScale { get; set; } = 1.0f;

    [JsonPropertyName("SelfIllumScale")]
    public float SelfIllumScale { get; set; } = 1.0f;

    [JsonPropertyName("StartActive")]
    public bool StartActive { get; set; } = true;

    [JsonPropertyName("EnablePluginLogs")]
    public bool EnablePluginLogs { get; set; } = true;
}