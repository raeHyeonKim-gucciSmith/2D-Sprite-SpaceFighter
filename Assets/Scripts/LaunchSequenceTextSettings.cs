using UnityEngine;

public sealed class LaunchSequenceTextSettings : MonoBehaviour
{
    [Header("Operator Messages")]
    [TextArea] public string radioCheck = "VIPER 01, IRIS.\n\nRADIO CHECK.";
    [TextArea] public string loudAndClear = "VIPER 01, LOUD & CLEAR.";
    [TextArea] public string radarCheck = "VIPER 01, RADAR CHECK.";
    [TextArea] public string radarSystemGreen = "VIPER 01,\nRADAR SYSTEM GREEN.";
    [TextArea] public string finalWeaponCheck = "VIPER 01, WEAPON CHECK.";
    [TextArea] public string weaponCheck = "VIPER 01,\nRADAR SYSTEM GREEN.\n\nWEAPON CHECK.";
    [TextArea] public string clearance = "VIPER 01,\nSYSTEMS ALL GREEN.\n\nCLEARED FOR LAUNCH.\n\nGOOD LUCK.";

    [Header("Radar Tutorial Text")]
    public string radarTitle = "UF-35 RADAR / LOCK-ON TUTORIAL";
    public string detectionLegend = "WHITE  90°  DETECTION";
    public string lockOnLegend = "GREEN  60°  LOCK-ON";
    public string radarDescription = "TARGET ENTERS 90°: DETECT  /  TARGET ENTERS 60°: LOCK ON";

    [Header("Launch Text")]
    public string missileWarning = "MISSILE LAUNCH";
    public string pressToLaunch = "PRESS LEFT CLICK";
    public string launching = "LAUNCHING";
    public string maximumThrust = "MAXIMUM THRUST";
    public string thrustInstruction = "HOLD TO BUILD THRUST  ·  HEADING LOCKED NORTH";
    public string maxThrustCruise = "MAX THRUST CRUISE";
}
