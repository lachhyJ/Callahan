namespace Callahan.Api.Models;

// How much heavy lower-body work a template carries. Not a training metric -
// it exists so the weekly planner can check the program's own spacing rules
// (nothing heavy the day before a field session; no two heavy days adjacent)
// without hardcoding template Ids. Set by hand per template; there is no way
// to derive it, since "heavy on legs" is about intent and load, not about
// which muscle groups the exercises touch.
public enum LowerBodyLoad
{
    None,
    Light,
    Heavy
}
