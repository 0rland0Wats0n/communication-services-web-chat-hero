/**import { connect } from 'react-redux';

import ConfigurationScreen from '../components/ConfigurationScreen';
import { addUserToThread, isValidThread, getEventInformation } from '../core/sideEffects';

const mapDispatchToProps = (dispatch: any) => ({
  setup: async (displayName: string, emoji: string) => {
    dispatch(addUserToThread(displayName, emoji));
  },
  isValidThread: async (threadId: string) => dispatch(isValidThread(threadId)),
  getEventInfo: async (eventID: string) => dispatch(getEventInformation(eventID))
});

export default connect(undefined, mapDispatchToProps)(ConfigurationScreen);**/
import { connect } from 'react-redux';
import CallingConfigurationScreen, { CallingConfigurationScreenProps, TokenResponse } from '../components/Configuration';
import { setCallAgent, setGroup } from '../core/actions/calls';
import { setVideoDeviceInfo, setAudioDeviceInfo } from '../core/actions/devices';
import { initCallClient, joinGroup, registerToCallAgent, updateDevices } from '../core/sideEffects';
import { setMic } from '../core/actions/controls';
import { State } from '../core/reducers';
import {
  AudioDeviceInfo,
  VideoDeviceInfo,
  LocalVideoStream,
  CallAgent,
  CallEndReason
} from '@azure/communication-calling';
import { CommunicationUserToken } from '@azure/communication-identity';
import { utils } from '../utils/utils';
import { AzureCommunicationTokenCredential } from '@azure/communication-common';
import { setUserId } from '../core/actions/sdk';

const mapStateToProps = (state: State, props: CallingConfigurationScreenProps) => ({
  deviceManager: state.devices.deviceManager,
  callAgent: state.calls.callAgent,
  group: state.calls.group,
  mic: state.controls.mic,
  screenWidth: props.screenWidth,
  audioDeviceInfo: state.devices.audioDeviceInfo,
  videoDeviceInfo: state.devices.videoDeviceInfo,
  videoDeviceList: state.devices.videoDeviceList,
  audioDeviceList: state.devices.audioDeviceList,
  cameraPermission: state.devices.cameraPermission,
  microphonePermission: state.devices.microphonePermission,
  joinGroup: async (callAgent: CallAgent, groupId: string, localVideoStream: LocalVideoStream): Promise<void> => {
    callAgent &&
      (await joinGroup(
        callAgent,
        {
          groupId
        },
        {
          videoOptions: {
            localVideoStreams: localVideoStream ? [localVideoStream] : undefined
          },
          audioOptions: { muted: !state.controls.mic }
        }
      ));
  },
  getToken: async (): Promise<TokenResponse> => {
    const tokenResponse: CommunicationUserToken = await utils.getTokenForUser();
    const userToken = tokenResponse.token;
    const userId = tokenResponse.user.communicationUserId;

    const tokenCredential = new AzureCommunicationTokenCredential({
      tokenRefresher: (): Promise<string> => {
        return utils.getRefreshedTokenForUser(userId);
      },
      refreshProactively: true,
      token: userToken
    });

    return {
      tokenCredential,
      userId
    };
  },
  createCallAgent: async (
    tokenCredential: AzureCommunicationTokenCredential,
    displayName: string
  ): Promise<CallAgent> => {
    const callClient = state.sdk.callClient;

    if (callClient === undefined) {
      throw new Error('CallClient is not initialized');
    }

    const callAgent: CallAgent = await callClient.createCallAgent(tokenCredential, { displayName });
    return callAgent;
  }
});

const mapDispatchToProps = (dispatch: any, props: CallingConfigurationScreenProps) => ({
  setMic: (mic: boolean): void => dispatch(setMic(mic)),
  setAudioDeviceInfo: (deviceInfo: AudioDeviceInfo): void => dispatch(setAudioDeviceInfo(deviceInfo)),
  setVideoDeviceInfo: (deviceInfo: VideoDeviceInfo): void => dispatch(setVideoDeviceInfo(deviceInfo)),
  setupCallClient: (unsupportedStateHandler: () => void): void => dispatch(initCallClient(unsupportedStateHandler)),
  registerToCallEvents: async (
    userId: string,
    callAgent: CallAgent,
    endCallHandler: (reason: CallEndReason) => void
  ): Promise<void> => {
    dispatch(setUserId(userId));
    dispatch(setCallAgent(callAgent));
    dispatch(registerToCallAgent(userId, callAgent, endCallHandler));
  },
  setGroup: (groupId: string): void => dispatch(setGroup(groupId)),
  updateDevices: (): void => dispatch(updateDevices())
});

const connector: any = connect(mapStateToProps, mapDispatchToProps);
export default connector(CallingConfigurationScreen);
