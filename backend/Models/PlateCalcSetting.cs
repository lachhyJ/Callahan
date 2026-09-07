namespace Callahan.Api.Models;

// A single device-local plate-calculator preference, promoted to the server so
// it survives the browser evicting localStorage (Safari's 7-day cap on
// script-writable storage for low-engagement sites, a native-app re-sign
// resetting the WKWebView data store) and follows the athlete across devices.
//
// Deliberately a generic key/value blob rather than typed columns: every value
// here is written and interpreted solely by the frontend (a list of plate
// sizes, a per-exercise bar weight, a per-exercise equipment-type override),
// and nothing server-side ever reasons about them. The key mirrors the
// localStorage key with the shared "callahan.plateCalc." prefix stripped, e.g.
// "availablePlates.kg", "customEquipment.11", "equipmentType.11". ValueJson is
// the raw JSON of the value exactly as the client would have put in
// localStorage.
public class PlateCalcSetting
{
    public required string Key { get; set; }
    public required string ValueJson { get; set; }
}
