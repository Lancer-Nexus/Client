// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

namespace LibreLancer.World
{
	public sealed record PIDControllerTransferState(double P, double I, double D, double Integral, double LastError)
	{
		public void Validate()
		{
			if (!double.IsFinite(P) || !double.IsFinite(I) || !double.IsFinite(D) ||
				!double.IsFinite(Integral) || !double.IsFinite(LastError))
				throw new System.IO.InvalidDataException("PID controller transfer state is invalid.");
		}
	}

	public class PIDController
	{
		public double P;
		public double I;
		public double D;

        private double integral;
        private double lastError;

		public PIDControllerTransferState CaptureTransferState() =>
			new(P, I, D, integral, lastError);

		public void RestoreTransferState(PIDControllerTransferState state)
		{
			state.Validate();
			P = state.P;
			I = state.I;
			D = state.D;
			integral = state.Integral;
			lastError = state.LastError;
		}

		public void Reset()
		{
			integral = lastError = 0;
		}

		public double Update(double setpoint, double actual, double timeFrame)
		{
            if (double.IsNaN(integral) || double.IsNaN(lastError)) Reset();
			double present = setpoint - actual;
			integral += present * timeFrame;
			double deriv = (present - lastError) / timeFrame;
			lastError = present;
			return present * P + integral * I + deriv * D;
		}
	}
}
