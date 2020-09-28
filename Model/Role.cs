using System;
namespace csharp_api.Model.Roles
{
    public enum Role
    {
        INNOCENT,
        TRAITOR,
        DETECTIVE
    }
    public static class RoleInfo
    {
        public const string Innocent = "INNOCENT";
        public const string Traitor = "TRAITOR";
        public const string Detective = "DETECTIVE";

        public static string GetName(Role role)
        {
            switch (role)
            {
                case Role.INNOCENT:
                    return RoleInfo.Innocent;
                case Role.TRAITOR:
                    return RoleInfo.Traitor;
                case Role.DETECTIVE:
                    return RoleInfo.Detective;
                default:
                    throw new ArgumentException();
            }
        }

        public static Role ToRole(string roleName)
        {
            switch (roleName)
            {
                case RoleInfo.Innocent:
                    return Role.INNOCENT;
                case RoleInfo.Traitor:
                    return Role.TRAITOR;
                case RoleInfo.Detective:
                    return Role.DETECTIVE;
                default:
                    throw new ArgumentException();
            }
        }

        public static bool CanSee(Role calling, Role checking)
        {
            return calling == Role.TRAITOR || checking == Role.DETECTIVE;
        }
    }
}