namespace Callahan.Api.Models;

// Which block of the program a gym template belongs to (program v4 has two gym layouts:
// Gym A/B in-season, Gym 1/2/3 with no club). Any is for anything that is not tied to
// a block - a custom template stays in the picker whichever mode is on. The same names
// double as the training mode (see TrainingModeStore), where Any is never a valid value.
public enum TemplateBlock
{
    Any = 0,
    InSeason = 1,
    NoClub = 2,
}
