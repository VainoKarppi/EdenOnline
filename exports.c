#include <stdint.h>

__declspec(dllexport) uint64_t RVExtensionFeatureFlags = 7;

__declspec(dllexport) uint64_t RVExtensionGetFeatureFlags()
{
    return RVExtensionFeatureFlags;
}