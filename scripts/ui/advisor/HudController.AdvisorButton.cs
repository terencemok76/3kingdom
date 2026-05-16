using Godot;

namespace ThreeKingdom.UI;

public partial class HudController
{
    private void EnsureAdvisorButton()
    {
        var commandButtons = GetNodeOrNull<GridContainer>("Root/LeftPanel/CommandButtons");
        if (commandButtons == null)
        {
            return;
        }

        _advisorButton = commandButtons.GetNodeOrNull<Button>("AdvisorButton");
        if (_advisorButton != null)
        {
            return;
        }

        _advisorButton = new Button
        {
            Name = "AdvisorButton",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None
        };

        if (_personnelButton != null)
        {
            CopyButtonTheme(_personnelButton, _advisorButton);
        }

        commandButtons.AddChild(_advisorButton);
    }
}
