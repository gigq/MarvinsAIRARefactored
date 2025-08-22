
using System.Numerics;

namespace MarvinsAIRARefactored.Classes;

public static class Butterworth
{
	// Design a digital Butterworth low-pass (bilinear transform) with normalized cutoff in (0,1), where 1.0 = Nyquist.
	// Returns (b, a) with a[0] = 1.0.
	public static (double[] b, double[] a) DesignLowpass( int order, double cutoff01 )
	{
		if ( order < 1 ) throw new ArgumentOutOfRangeException( nameof( order ) );
		if ( !( cutoff01 > 0.0 && cutoff01 < 1.0 ) ) throw new ArgumentOutOfRangeException( nameof( cutoff01 ), "cutoff01 must be in (0,1)." );

		// Prewarp for bilinear transform (fs normalized to 2)
		var wc = Math.Tan( Math.PI * cutoff01 );

		// Analog poles for Butterworth of unity cutoff, scaled by wc
		var poles = new Complex[ order ];
		for ( var k = 0; k < order; k++ )
		{
			var theta = ( Math.PI * ( 2.0 * k + order + 1 ) ) / ( 2.0 * order );
			var pole = wc * new Complex( -Math.Sin( theta ), Math.Cos( theta ) ); // LHP
			poles[ k ] = pole;
		}

		var bAll = new double[] { 1.0 };
		var aAll = new double[] { 1.0 };

		var poleIndex = 0;
		while ( poleIndex < order )
		{
			if ( poleIndex <= order - 2 )
			{
				var p1 = poles[ poleIndex ];
				var p2 = Complex.Conjugate( p1 );
				MultiplySection( ref bAll, ref aAll, BilinearLPFromAnalogPolePair( p1, p2 ) );
				poleIndex += 2;
			}
			else
			{
				var p = poles[ poleIndex ];
				MultiplySection( ref bAll, ref aAll, BilinearLPFromAnalogPole( p ) );
				poleIndex += 1;
			}
		}

		// Normalize DC gain
		var dcGain = PolyVal( bAll, 1.0 ) / PolyVal( aAll, 1.0 );
		for ( var i = 0; i < bAll.Length; i++ ) bAll[ i ] /= dcGain;

		return (bAll, aAll);

		static (double[] b, double[] a) BilinearLPFromAnalogPole( Complex p )
		{
			// First‑order section via bilinear transform (fs=2)
			// Final normalized form: a[0] == 1
			var k = 2.0;
			// Derived by substitution s = 2(1 - z^-1)/(1 + z^-1) and grouping:
			var A0 = ( k - p.Real );
			var A1 = ( -k - p.Real );
			var B0 = 1.0;
			var B1 = 1.0;

			var scale = 1.0 / A0;
			return (new[] { B0 * scale, B1 * scale }, new[] { 1.0, A1 * scale });
		}

		static (double[] b, double[] a) BilinearLPFromAnalogPolePair( Complex p1, Complex p2 )
		{
			// Standard low‑pass biquad after bilinear transform (fs=2)
			var k = 2.0;
			var k2 = k * k;

			var s1 = p1 + p2;           // sum of poles
			var s0 = p1 * p2;           // product of poles (real)

			var A0 = ( k2 + k * s1.Real + s0.Real );
			var A1 = ( 2.0 * ( s0.Real - k2 ) );
			var A2 = ( k2 - k * s1.Real + s0.Real );

			var B0 = 1.0;
			var B1 = 2.0;
			var B2 = 1.0;

			var scale = 1.0 / A0;
			return (new[] { B0 * scale, B1 * scale, B2 * scale },
					new[] { 1.0, A1 * scale, A2 * scale });
		}

		static void MultiplySection( ref double[] bAll, ref double[] aAll, (double[] b, double[] a) sec )
		{
			bAll = Convolve( bAll, sec.b );
			aAll = Convolve( aAll, sec.a );
		}

		static double[] Convolve( double[] x, double[] y )
		{
			var nx = x.Length;
			var ny = y.Length;
			var result = new double[ nx + ny - 1 ];
			for ( var i = 0; i < nx; i++ )
				for ( var j = 0; j < ny; j++ )
					result[ i + j ] += x[ i ] * y[ j ];
			return result;
		}

		static double PolyVal( double[] p, double z )
		{
			var acc = 0.0;
			for ( var i = 0; i < p.Length; i++ )
				acc = acc * z + p[ i ];
			return acc;
		}
	}

	// Direct Form II Transposed (single causal pass)
	public static void FilterInPlace( double[] b, double[] a, float[] x, float[] y )
	{
		if ( a.Length == 0 || b.Length == 0 ) throw new ArgumentException( "Empty coeffs." );
		if ( Math.Abs( a[ 0 ] - 1.0 ) > 1e-9 ) throw new ArgumentException( "a[0] must be 1.0." );
		if ( y.Length < x.Length ) throw new ArgumentException( "Output array too small." );

		var order = Math.Max( a.Length, b.Length ) - 1;
		var state = new double[ order ];

		for ( var sampleIndex = 0; sampleIndex < x.Length; sampleIndex++ )
		{
			var input = x[ sampleIndex ];
			var w0 = input - SumA( a, state );
			var output = SumB( b, w0, state );
			y[ sampleIndex ] = (float) output;
			UpdateState( state, w0 );
		}

		static double SumA( double[] a, double[] z )
		{
			var acc = 0.0;
			for ( var i = 1; i < a.Length; i++ )
			{
				var zi = i - 1;
				if ( zi < z.Length ) acc += a[ i ] * z[ zi ];
			}
			return acc;
		}

		static double SumB( double[] b, double w0, double[] z )
		{
			var acc = b[ 0 ] * w0;
			for ( var i = 1; i < b.Length; i++ )
			{
				var zi = i - 1;
				if ( zi < z.Length ) acc += b[ i ] * z[ zi ];
			}
			return acc;
		}

		static void UpdateState( double[] z, double w0 )
		{
			for ( var i = z.Length - 1; i >= 1; i-- ) z[ i ] = z[ i - 1 ];
			if ( z.Length > 0 ) z[ 0 ] = w0;
		}
	}

	// Zero‑phase forward–backward filter with reflection padding (SciPy‑like)
	public static float[] Filtfilt( double[] b, double[] a, float[] x )
	{
		var padLen = 3 * ( Math.Max( a.Length, b.Length ) - 1 );
		padLen = Math.Clamp( padLen, 0, Math.Max( 0, x.Length - 1 ) );

		var xPadded = ReflectPad( x, padLen );

		var yForward = new float[ xPadded.Length ];
		FilterInPlace( b, a, xPadded, yForward );

		Array.Reverse( yForward );
		var yBackward = new float[ yForward.Length ];
		FilterInPlace( b, a, yForward, yBackward );
		Array.Reverse( yBackward );

		// remove padding
		var y = new float[ x.Length ];
		Array.Copy( yBackward, padLen, y, 0, x.Length );
		return y;

		static float[] ReflectPad( float[] src, int p )
		{
			if ( p <= 0 ) return src.ToArray();
			var n = src.Length;
			var dst = new float[ n + 2 * p ];

			for ( var i = 0; i < p; i++ ) dst[ i ] = src[ p - i ];          // left reflect
			Array.Copy( src, 0, dst, p, n );
			for ( var i = 0; i < p; i++ ) dst[ p + n + i ] = src[ n - 2 - i ]; // right reflect

			return dst;
		}
	}

	// Convenience: one‑shot zero‑phase low‑pass
	public static float[] FiltfiltLowpass( float[] x, int order, double cutoff01 )
	{
		var (b, a) = DesignLowpass( order, cutoff01 );
		return Filtfilt( b, a, x );
	}
}
