using System.ComponentModel.DataAnnotations;

namespace FitLog.ViewModels;

public class SetUsernameViewModel
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "Letters and numbers only.")]
    public string Username { get; set; } = string.Empty;
}

public class DeleteAccountConfirmViewModel
{
    [Required]
    public string ConfirmText { get; set; } = string.Empty;
}

public class ManageRolesRoleRowViewModel
{
    public string RoleName { get; set; } = string.Empty;
    public bool IsAssigned { get; set; }
}

public class ManageRolesPageViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<ManageRolesRoleRowViewModel> Roles { get; set; } = new();
}
