namespace Migration.Intelligence.Design.Models;

public enum TableRole
{
    Owned = 0,
    Shared = 1,
    Referenced = 2,
    Ambiguous = 3
}
