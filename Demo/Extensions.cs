using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Clinic_Consultation_System;

public static class Extensions
{
    public static bool IsAjax(this HttpRequest request)
    {
        return request.Headers.XRequestedWith == "XMLHttpRequest";
    }
    public static bool IsValid(this ModelStateDictionary ms, string key)
    {
        return ms.GetFieldValidationState(key) == ModelValidationState.Valid;
    }
}