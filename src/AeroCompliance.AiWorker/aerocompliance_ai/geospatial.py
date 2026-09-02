def require_georeferencing(crs, affine):
    if not crs or not affine:
        raise ValueError("Raster CRS and affine transform are required; pixel coordinates cannot be used as map coordinates.")
