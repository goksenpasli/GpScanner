using System;
using TwainWpf.TwainNative;

namespace TwainWpf {
    public class Capability {
        public Capability(Capabilities capability, TwainType twainType, Identity applicationId, Identity sourceId) {
            _capability = capability;
            _applicationId = applicationId;
            _sourceId = sourceId;
            _twainType = twainType;
        }

        public static bool GetBoolCapability(Capabilities capability, Identity applicationId,
            Identity sourceId) {
            Capability c = new Capability(capability, TwainType.Int16, applicationId, sourceId);
            BasicCapabilityResult capResult = c.GetBasicValue();

            // Check that the device supports the capability
            return capResult.ConditionCode != ConditionCode.Success
                ? throw new TwainException(string.Format("Unsupported capability {0}", capability),
                    capResult.ErrorCode, capResult.ConditionCode)
                : capResult.BoolValue;
        }

        public static int SetBasicCapability(Capabilities capability, int rawValue, TwainType twainType, Identity applicationId, Identity sourceId) {
            Capability c = new Capability(capability, twainType, applicationId, sourceId);
            BasicCapabilityResult basicValue = c.GetBasicValue();

            // Check that the device supports the capability
            if (basicValue.ConditionCode != ConditionCode.Success) {
                throw new TwainException(string.Format("Unsupported capability {0}", capability), basicValue.ErrorCode, basicValue.ConditionCode);
            }
            if (basicValue.RawBasicValue == rawValue) {
                // Value is already set
                return rawValue;
            }

            // TODO: Check the set of Available Values that are supported by the Source for that
            // capability.

            //if (value in set of available values)
            //{
            c.SetValue(rawValue);

            //}

            // Verify that the new values have been accepted by the Source.
            basicValue = c.GetBasicValue();

            // Check that the device supports the capability
            return basicValue.ConditionCode != ConditionCode.Success
                ? throw new TwainException(string.Format("Unexpected failure verifying capability {0}", capability), basicValue.ErrorCode, basicValue.ConditionCode)
                : basicValue.RawBasicValue;
        }

        public static short SetCapability(Capabilities capability, short value, Identity applicationId, Identity sourceId) {
            return (short)SetBasicCapability(capability, value, TwainType.Int16, applicationId, sourceId);
        }

        public static void SetCapability(Capabilities capability, bool value, Identity applicationId, Identity sourceId) {
            Capability c = new Capability(capability, TwainType.Bool, applicationId, sourceId);
            BasicCapabilityResult capResult = c.GetBasicValue();

            // Check that the device supports the capability
            if (capResult.ConditionCode != ConditionCode.Success) {
                throw new TwainException(string.Format("Unsupported capability {0}", capability),
                    capResult.ErrorCode, capResult.ConditionCode);
            }

            if (capResult.BoolValue == value) {
                // Value is already set
                return;
            }

            c.SetValue(value);

            // Verify that the new values have been accepted by the Source.
            capResult = c.GetBasicValue();

            // Check that the device supports the capability
            if (capResult.ConditionCode != ConditionCode.Success) {
                throw new TwainException(string.Format("Unexpected failure verifying capability {0}", capability),
                    capResult.ErrorCode, capResult.ConditionCode);
            }
            else if (capResult.BoolValue != value) {
                throw new TwainException(string.Format("Failed to set value for capability {0}", capability),
                    capResult.ErrorCode, capResult.ConditionCode);
            }
        }

        public BasicCapabilityResult GetBasicValue() {
            CapabilityOneValue oneValue = new CapabilityOneValue(_twainType, 0);
            TwainCapability twainCapability = TwainCapability.From(_capability, oneValue);

            TwainResult result = Twain32Native.DsCapability(
                    _applicationId,
                    _sourceId,
                    DataGroup.Control,
                    DataArgumentType.Capability,
                    Message.Get,
                    twainCapability);

            if (result != TwainResult.Success) {
                ConditionCode conditionCode = GetStatus();

                return new BasicCapabilityResult() {
                    ConditionCode = conditionCode,
                    ErrorCode = result
                };
            }

            twainCapability.ReadBackValue();

            return new BasicCapabilityResult() {
                RawBasicValue = oneValue.Value
            };
        }

        public void SetValue(short value) {
            SetValue<short>(value);
        }

        protected ConditionCode GetStatus() {
            return DataSourceManager.GetConditionCode(_applicationId, _sourceId);
        }

        protected void SetValue<T>(T value) {
            int rawValue = Convert.ToInt32(value);
            CapabilityOneValue oneValue = new CapabilityOneValue(_twainType, rawValue);
            TwainCapability twainCapability = TwainCapability.From(_capability, oneValue);

            TwainResult result = Twain32Native.DsCapability(
                _applicationId,
                _sourceId,
                DataGroup.Control,
                DataArgumentType.Capability,
                Message.Set,
                twainCapability);

            if (result == TwainResult.Success) {
                return;
            }
            if (result == TwainResult.Failure) {
                throw new TwainException("Failed to set capability.", result, GetStatus());
            }
            if (result != TwainResult.CheckStatus) {
                throw new TwainException("Failed to set capability.", result);
            }
        }

        private readonly Identity _applicationId;

        private readonly Capabilities _capability;

        private readonly Identity _sourceId;

        private readonly TwainType _twainType;
    }
}