
namespace MarvinsAIRARefactored.Classes;

public static class Butterworth
{
	// Represents one biquad section: b0 + b1 z^-1 + b2 z^-2 over 1 + a1 z^-1 + a2 z^-2
	private struct Biquad
	{
		public double b0, b1, b2;
		public double a1, a2; // a0 normalized to 1
	}

	// Public one-shot: design Nth-order Butterworth and apply filtfilt.
	public static float[] FiltfiltLowpass( float[] x, int order, double cutoff01 )
	{
		if ( x == null || x.Length == 0 ) return Array.Empty<float>();
		if ( !( cutoff01 > 0.0 && cutoff01 < 1.0 ) )
			throw new ArgumentOutOfRangeException( nameof( cutoff01 ), "cutoff01 must be in (0,1)." );
		if ( order < 1 ) throw new ArgumentOutOfRangeException( nameof( order ) );

		var sos = DesignButterworthLowpass( order, cutoff01 );

		// reflect pad (SciPy-style length)
		var padLen = 3 * 2; // per-biquad state length is small; 6 samples is plenty and robust for most sets
		padLen = Math.Clamp( padLen, 0, Math.Max( 0, x.Length - 1 ) );

		var xPad = ReflectPad( x, padLen );

		// forward
		var y = xPad.ToArray();
		foreach ( var s in sos ) FilterBiquadInPlace( s, y );

		// backward
		Array.Reverse( y );
		foreach ( var s in sos ) FilterBiquadInPlace( s, y );
		Array.Reverse( y );

		// remove padding
		var outArr = new float[ x.Length ];
		Array.Copy( y, padLen, outArr, 0, x.Length );
		return outArr;
	}

	// ================== Design ==================
	private static Biquad[] DesignButterworthLowpass( int order, double cutoff01 )
	{
		// Prewarp analog frequency (fs normalized so Nyquist=1.0 -> fs=2.0)
		var w0 = Math.Tan( Math.PI * cutoff01 );

		// Number of biquads
		var nBiquads = order / 2;
		var hasFirstOrder = ( order % 2 ) == 1;

		var sos = new Biquad[ nBiquads + ( hasFirstOrder ? 1 : 0 ) ];
		var idx = 0;

		// Build 2nd-order sections
		for ( int k = 1; k <= nBiquads; k++ )
		{
			// Butterworth angles
			// theta_k = PI * (2k + order - 1) / (2*order) === PI/(2N) * (2k-1)
			var theta = Math.PI * ( 2.0 * k - 1.0 ) / ( 2.0 * order );

			// Analog prototype (unit cutoff) second-order denominator: s^2 + s*(2*sin(theta)) + 1
			var alpha = 2.0 * Math.Sin( theta );   // damping term at unit cutoff

			// Now scale by w0 (prewarped). Analog LP denom becomes: s^2 + s*(alpha*w0) + w0^2
			// Bilinear transform with fs=2: s = 2 (1 - z^-1)/(1 + z^-1)
			// After substitution and normalization (a0=1), digital biquad coefficients are:

			// Helper constants
			var K = w0;
			var KK = K * K;

			// Bilinear transform mapping
			// Using cookbook-form derivation (RBJ) adapted for Butterworth damping:
			var norm = 4.0 + 2.0 * alpha * K + KK;

			var b0 = KK / norm;
			var b1 = 2.0 * KK / norm;
			var b2 = KK / norm;

			var a1 = ( 2.0 * KK - 8.0 ) / norm;
			var a2 = ( 4.0 - 2.0 * alpha * K + KK ) / norm;

			sos[ idx++ ] = new Biquad { b0 = b0, b1 = b1, b2 = b2, a1 = a1, a2 = a2 };
		}

		// If odd order, append a first-order section as a "degenerate" biquad (b2=0, a2=0)
		if ( hasFirstOrder )
		{
			// Analog 1st order: s + 1, scaled by w0 → s + w0
			// Digital (bilinear, fs=2):
			var K = w0;
			var norm = ( 2.0 + K );

			var b0 = K / norm;
			var b1 = K / norm;
			var b2 = 0.0;

			var a1 = ( K - 2.0 ) / norm;
			var a2 = 0.0;

			sos[ idx ] = new Biquad { b0 = b0, b1 = b1, b2 = b2, a1 = a1, a2 = a2 };
		}

		// Normalize overall DC gain to 1 (evaluate cascade at z=1)
		var dcGain = 1.0;
		foreach ( var s in sos )
		{
			var num = s.b0 + s.b1 + s.b2;
			var den = 1.0 + s.a1 + s.a2;
			dcGain *= ( num / den );
		}
		var g = 1.0 / dcGain;
		for ( int i = 0; i < sos.Length; i++ )
		{
			// distribute gain to the first section to avoid scaling issues
			if ( i == 0 )
				sos[ i ] = new Biquad { b0 = sos[ i ].b0 * g, b1 = sos[ i ].b1 * g, b2 = sos[ i ].b2 * g, a1 = sos[ i ].a1, a2 = sos[ i ].a2 };
		}

		return sos;
	}

	// ================== Filtering ==================
	private static void FilterBiquadInPlace( Biquad s, float[] x )
	{
		double z1 = 0.0, z2 = 0.0; // DF2T state
		for ( int n = 0; n < x.Length; n++ )
		{
			var w = x[ n ] - s.a1 * z1 - s.a2 * z2;
			var y = s.b0 * w + s.b1 * z1 + s.b2 * z2;
			z2 = z1;
			z1 = w;
			x[ n ] = (float) y;
		}
	}

	private static float[] ReflectPad( float[] src, int p )
	{
		if ( p <= 0 ) return src.ToArray();
		var n = src.Length;
		var dst = new float[ n + 2 * p ];

		// left reflect
		for ( var i = 0; i < p; i++ )
			dst[ i ] = src[ p - i ];
		// copy
		Array.Copy( src, 0, dst, p, n );
		// right reflect
		for ( var i = 0; i < p; i++ )
			dst[ p + n + i ] = src[ n - 2 - i ];

		return dst;
	}
}
