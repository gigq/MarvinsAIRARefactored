
using Accord.Math;

namespace MarvinsAIRARefactored.Classes;

public class YawRateModel( int[] steeringWheelAnglesInDegrees, float[,] yawRateDataInDegrees, int maxSpeed )
{
	private readonly int[] _steeringWheelAnglesInDegrees = steeringWheelAnglesInDegrees;
	private readonly float[,] _yawRateDataInDegrees = yawRateDataInDegrees;
	private readonly int _maxSpeed = maxSpeed;

	public float MaxYawPredictionError = 1.5f; // adjustable threshold

	private readonly int _minStartingMagnitude = 150;

	public (float[] yawRateCoefficients, float[] speedCoefficients) FitWithProgressiveRefinement( bool leftTurn )
	{
		var usedAngles = new List<float>();
		var usedMaxYawRates = new List<float>();
		var usedCorrespondingSpeeds = new List<float>();

		var initialAngles = GetSortedAngles( leftTurn ).Where( a => Math.Abs( a ) >= _minStartingMagnitude ).ToList();

		foreach ( var angle in initialAngles )
		{
			var (maxYawRate, correspondingSpeed) = GetMaxYawRateAtAngle( angle, leftTurn );

			if ( correspondingSpeed >= 0 )
			{
				usedAngles.Add( angle );
				usedMaxYawRates.Add( maxYawRate );
				usedCorrespondingSpeeds.Add( correspondingSpeed );
			}
		}

		var remainingAngles = GetSortedAngles( leftTurn ).Where( a => Math.Abs( a ) < _minStartingMagnitude ).ToList();

		foreach ( var angle in remainingAngles )
		{
			var yawRateCoefficients = FitQuadratic( [ .. usedAngles ], [ .. usedMaxYawRates ] );
			var expectedYawRate = Predict( yawRateCoefficients, angle );
			var yawRatePeakCandidates = GetYawRatePeaksAtAngle( angle, out var speeds, leftTurn );

			if ( yawRatePeakCandidates.Count > 0 )
			{
				var bestCandidateIndex = 0;
				var bestCandidateError = float.MaxValue;

				for ( var candidateIndex = 0; candidateIndex < yawRatePeakCandidates.Count; candidateIndex++ )
				{
					var candidateError = MathF.Abs( yawRatePeakCandidates[ candidateIndex ] - expectedYawRate );

					if ( candidateError < bestCandidateError )
					{
						bestCandidateError = candidateError;
						bestCandidateIndex = candidateIndex;
					}
				}

				if ( bestCandidateError <= MaxYawPredictionError )
				{
					usedAngles.Add( angle );
					usedMaxYawRates.Add( yawRatePeakCandidates[ bestCandidateIndex ] );
					usedCorrespondingSpeeds.Add( speeds[ bestCandidateIndex ] );
				}
			}
		}

		var finalYawCoefficients = FitQuadratic( [ .. usedAngles ], [ .. usedMaxYawRates ] );
		var finalSpeedCoefficients = FitQuadratic( [ .. usedAngles ], [ .. usedCorrespondingSpeeds ] );

		return (finalYawCoefficients, finalSpeedCoefficients);
	}

	private List<int> GetSortedAngles( bool leftTurn )
	{
		return [ .. _steeringWheelAnglesInDegrees.Where( angle => leftTurn ? angle < 0 : angle > 0 ).OrderBy( angle => Math.Abs( angle ) ).Reverse() ];
	}

	private (float yaw, int speed) GetMaxYawRateAtAngle( int angle, bool leftTurn = true )
	{
		var angleIndex = Array.IndexOf( _steeringWheelAnglesInDegrees, angle );

		if ( angleIndex < 0 )
		{
			return (0f, -1);
		}

		var maxYaw = float.MinValue;
		var speedAtMax = -1;

		for ( var speed = 0; speed <= _maxSpeed; speed++ )
		{
			var yaw = _yawRateDataInDegrees[ angleIndex, speed ];

			if ( !leftTurn )
			{
				yaw = -yaw;
			}

			if ( yaw > maxYaw )
			{
				maxYaw = yaw;
				speedAtMax = speed;
			}
		}

		return (maxYaw, speedAtMax);
	}

	private List<float> GetYawRatePeaksAtAngle( int angle, out List<int> speeds, bool leftTurn = true )
	{
		speeds = [];

		var peaks = new List<float>();
		var angleIndex = Array.IndexOf( _steeringWheelAnglesInDegrees, angle );

		if ( angleIndex < 0 )
		{
			return peaks;
		}

		for ( var speed = 1; speed < _maxSpeed - 1; speed++ )
		{
			var prev = _yawRateDataInDegrees[ angleIndex, speed - 1 ];
			var curr = _yawRateDataInDegrees[ angleIndex, speed ];
			var next = _yawRateDataInDegrees[ angleIndex, speed + 1 ];

			if ( !leftTurn )
			{
				prev = -prev;
				curr = -curr;
				next = -next;
			}

			if ( ( curr > prev ) && ( curr > next ) )
			{
				peaks.Add( curr );
				speeds.Add( speed );
			}
		}

		return peaks;
	}

	public static float[] FitQuadratic( float[] x, float[] y )
	{
		var length = x.Length;

		var designMatrix = new float[ length, 3 ];

		for ( var i = 0; i < length; i++ )
		{
			designMatrix[ i, 0 ] = 1f;              // Constant term
			designMatrix[ i, 1 ] = x[ i ];          // Linear term
			designMatrix[ i, 2 ] = x[ i ] * x[ i ]; // Quadratic term
		}

		// Solve via normal equations: (X^T * X) * coeffs = X^T * y

		float[,] xt = designMatrix.Transpose();
		float[,] xtx = xt.Dot( designMatrix );
		float[] xty = xt.Dot( y );

		float[] coefficients = xtx.Solve( xty );

		return coefficients;
	}

	public static float Predict( float[] coefficients, float angle )
	{
		return coefficients[ 0 ] + coefficients[ 1 ] * angle + coefficients[ 2 ] * angle * angle;
	}
}
