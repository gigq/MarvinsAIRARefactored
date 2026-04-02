#include <cstddef>
#include <iostream>

#include "C:/Users/Justin/Apps/LMUFFB/src/io/lmu_sm_interface/LmuSharedMemoryWrapper.h"

int main()
{
    std::cout << "sizeof(SharedMemoryLayout)=" << sizeof(SharedMemoryLayout) << "\n";
    std::cout << "offsetof(SharedMemoryLayout,data)=" << offsetof(SharedMemoryLayout, data) << "\n";
    std::cout << "offsetof(SharedMemoryObjectOut,generic)=" << offsetof(SharedMemoryObjectOut, generic) << "\n";
    std::cout << "offsetof(SharedMemoryObjectOut,paths)=" << offsetof(SharedMemoryObjectOut, paths) << "\n";
    std::cout << "offsetof(SharedMemoryObjectOut,scoring)=" << offsetof(SharedMemoryObjectOut, scoring) << "\n";
    std::cout << "offsetof(SharedMemoryObjectOut,telemetry)=" << offsetof(SharedMemoryObjectOut, telemetry) << "\n";
    std::cout << "sizeof(SharedMemoryTelemetryData)=" << sizeof(SharedMemoryTelemetryData) << "\n";
    std::cout << "offsetof(SharedMemoryTelemetryData,activeVehicles)=" << offsetof(SharedMemoryTelemetryData, activeVehicles) << "\n";
    std::cout << "offsetof(SharedMemoryTelemetryData,playerVehicleIdx)=" << offsetof(SharedMemoryTelemetryData, playerVehicleIdx) << "\n";
    std::cout << "offsetof(SharedMemoryTelemetryData,playerHasVehicle)=" << offsetof(SharedMemoryTelemetryData, playerHasVehicle) << "\n";
    std::cout << "offsetof(SharedMemoryTelemetryData,telemInfo)=" << offsetof(SharedMemoryTelemetryData, telemInfo) << "\n";
    std::cout << "sizeof(TelemInfoV01)=" << sizeof(TelemInfoV01) << "\n";
    std::cout << "offsetof(TelemInfoV01,mID)=" << offsetof(TelemInfoV01, mID) << "\n";
    std::cout << "offsetof(TelemInfoV01,mDeltaTime)=" << offsetof(TelemInfoV01, mDeltaTime) << "\n";
    std::cout << "offsetof(TelemInfoV01,mLapNumber)=" << offsetof(TelemInfoV01, mLapNumber) << "\n";
    std::cout << "offsetof(TelemInfoV01,mVehicleName)=" << offsetof(TelemInfoV01, mVehicleName) << "\n";
    std::cout << "offsetof(TelemInfoV01,mTrackName)=" << offsetof(TelemInfoV01, mTrackName) << "\n";
    std::cout << "offsetof(TelemInfoV01,mLocalVel)=" << offsetof(TelemInfoV01, mLocalVel) << "\n";
    std::cout << "offsetof(TelemInfoV01,mLocalAccel)=" << offsetof(TelemInfoV01, mLocalAccel) << "\n";
    std::cout << "offsetof(TelemInfoV01,mLocalRot)=" << offsetof(TelemInfoV01, mLocalRot) << "\n";
    std::cout << "offsetof(TelemInfoV01,mGear)=" << offsetof(TelemInfoV01, mGear) << "\n";
    std::cout << "offsetof(TelemInfoV01,mEngineRPM)=" << offsetof(TelemInfoV01, mEngineRPM) << "\n";
    std::cout << "offsetof(TelemInfoV01,mUnfilteredThrottle)=" << offsetof(TelemInfoV01, mUnfilteredThrottle) << "\n";
    std::cout << "offsetof(TelemInfoV01,mUnfilteredBrake)=" << offsetof(TelemInfoV01, mUnfilteredBrake) << "\n";
    std::cout << "offsetof(TelemInfoV01,mUnfilteredSteering)=" << offsetof(TelemInfoV01, mUnfilteredSteering) << "\n";
    std::cout << "offsetof(TelemInfoV01,mUnfilteredClutch)=" << offsetof(TelemInfoV01, mUnfilteredClutch) << "\n";
    std::cout << "offsetof(TelemInfoV01,mSteeringShaftTorque)=" << offsetof(TelemInfoV01, mSteeringShaftTorque) << "\n";
    std::cout << "offsetof(TelemInfoV01,mEngineMaxRPM)=" << offsetof(TelemInfoV01, mEngineMaxRPM) << "\n";
    std::cout << "offsetof(TelemInfoV01,mMaxGears)=" << offsetof(TelemInfoV01, mMaxGears) << "\n";
    std::cout << "offsetof(TelemInfoV01,mPhysicalSteeringWheelRange)=" << offsetof(TelemInfoV01, mPhysicalSteeringWheelRange) << "\n";
    std::cout << "offsetof(TelemInfoV01,mABSActive)=" << offsetof(TelemInfoV01, mABSActive) << "\n";
    std::cout << "offsetof(TelemInfoV01,mTCActive)=" << offsetof(TelemInfoV01, mTCActive) << "\n";
    return 0;
}
