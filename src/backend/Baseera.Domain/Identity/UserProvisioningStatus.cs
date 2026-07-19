namespace Baseera.Domain.Identity;

public enum UserProvisioningStatus
{
    /// <summary>Pre-provisioned and allowed to sign in when IsActive.</summary>
    Active = 0,
    /// <summary>Reserved for future pending workflow; rejected at login under pre-provisioned policy.</summary>
    Pending = 1,
    Disabled = 2
}
