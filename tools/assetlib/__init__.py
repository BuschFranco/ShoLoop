"""Shared building blocks for the asset generators in tools/.

Nothing here writes an asset by itself -- each gen_*.py script owns one asset family and reaches in
here for the parts. The split exists because the generators genuinely share primitives (the sound
effects and the music are built from the same oscillators) and constants (every script needs the
game's palette), not to have layers for their own sake.
"""
