def build(path, all_keys, total_frames, reload=True):
    """fresh load of the saved project, extend timeline, write key poses, clean strays, set interpolation, bake sword."""
    global ns, ds
    sm = app.get_scene_manager()
    if reload:
        old = sm.current_scene()
        new = sm.create_application_scene(); sm.set_current_scene(new)
        csc.app.ProjectLoader.load_from(path, new.domain_scene())
        if old.name().startswith('Longsword_Attack1') or old.name().startswith('untitled'):
            sm.remove_application_scene(old)
        sm.set_current_scene(new)
    ns = sm.current_scene(); ds = ns.domain_scene()
    mv = ds.model_viewer(); lv = ds.layers_viewer()
    def ext(model, update, su):
        le = model.layers_editor(); empty = csc.layers.layer.Section()
        for lid in lv.all_layer_ids():
            if not lid.is_null(): le.set_section(empty, total_frames, lid)
        model.fit_animation_size_by_layers(); su.generate_update()
    r = [ds.modify_update("extend timeline", ext)]
    sols = solve_all(all_keys)
    r.append(write_keys2(ds, sols, "Attack1 key poses"))
    def clean(model, update, su):
        le = model.layers_editor()
        for lid in lv.all_layer_ids():
            if lid.is_null(): continue
            ks = list(lv.layer(lid).key_frame_indices())
            for fr in (1, total_frames):
                if fr in ks and fr not in all_keys and len(ks) > 2: le.unset_section(fr, lid)
        su.generate_update()
    r.append(ds.modify_update("remove stray keys", clean))
    I = csc.layers.layer.Interpolation
    def interp(model, update, su):
        le = model.layers_editor()
        for lid in lv.all_layer_ids():
            if lid.is_null(): continue
            secs = dict(lv.layer(lid).sections)
            if not secs: continue
            last = max(secs)
            for pos, sec in secs.items():
                if pos < last: sec.interval.interpolation = I.CLAMPED_BEZIER
            le.set_sections(secs, lid)
        su.generate_update()
    r.append(ds.modify_update("clamped bezier", interp))
    return r, sols
