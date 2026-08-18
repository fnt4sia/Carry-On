using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Luggage))]
public class LuggageVisualEffects : MonoBehaviour
{
    [Header("Grab Trail")]
    [SerializeField] private TrailRenderer grabTrail;

    [Header("Trail Time Scaling")]
    [SerializeField, Min(0f)] private float trailVelocityMin = 0f;
    [SerializeField, Min(0f)] private float trailVelocityMax = 20f;
    [SerializeField, Min(0f)] private float trailTimeMin = 0.1f;
    [SerializeField, Min(0f)] private float trailTimeMax = 0.6f;

    [Header("Hit Particle")]
    [SerializeField] private List<ParticleSystem> hitParticle = new List<ParticleSystem>();
    private int currentHitParticleIndex = 0;
    [SerializeField, Min(0f)] private float hitParticleForceThreshold = 300f;
    [SerializeField, Min(0f)] private float hitParticleCooldown = 0.2f; // min seconds between particle triggers

    [Header("Hit Particle Scaling")]
[Header("Hit Particle Scaling")]
[SerializeField, Min(0f)] private float particleScaleForceMin = 0f;   // force → small particle
[SerializeField, Min(0f)] private float particleScaleForceMax = 500f; // force → big particle
[SerializeField, Min(0f)] private float particleSizeAtMinForce = 2f;  // absolute size at min force
[SerializeField, Min(0f)] private float particleSizeAtMaxForce = 12f; // absolute size at max force

    private Luggage luggage;
    private Rigidbody luggageRb;
    private bool wasGrabbedLastFrame;
    private bool wasThrown;
    private bool tutorialVisualsApplied;
    private float baseStartSizeMin;
    private float baseStartSizeMax;
    private float baseTrailTime;
    private float nextHitParticleTime;

    private void Awake()
    {
        luggage = GetComponent<Luggage>();
        luggageRb = GetComponent<Rigidbody>();
        SetTrailActive(false);

        if (hitParticle.Count > 0)
        {
            var main = hitParticle[0].main;
            baseStartSizeMin = main.startSize.constantMin;
            baseStartSizeMax = main.startSize.constantMax;
        }

        if (grabTrail != null)
            baseTrailTime = grabTrail.time;
    }

    private void OnEnable()
    {
        // A pooled bag replays its playOnAwake particles every time it is re-enabled,
        // so the tutorial silencing has to run again on each rent.
        tutorialVisualsApplied = false;
    }

    private void Update()
    {
        // Menu / tutorial bags are scenery: no smoke, no throw trail. The flag is set by
        // the spawner after Awake has already run, so it is applied here instead.
        if (luggage.isTutorialLuggage)
        {
            if (!tutorialVisualsApplied)
                SilenceEffectsForTutorial();
            return;
        }

        bool isGrabbedNow = luggage.GetIsGrabbed();

        if (!wasGrabbedLastFrame && isGrabbedNow)
        {
            wasThrown = false;
            SetTrailActive(false);
        }
        else if (wasGrabbedLastFrame && !isGrabbedNow)
        {
            SetTrailActive(true);
            wasThrown = true;
            nextHitParticleTime = 0f; // reset cooldown on each new throw
        }

        if (isGrabbedNow)
            UpdateTrailTime();

        wasGrabbedLastFrame = isGrabbedNow;
    }

    private void UpdateTrailTime()
    {
        if (grabTrail == null || luggageRb == null) return;

        float speed = luggageRb.linearVelocity.magnitude;
        float t = Mathf.InverseLerp(trailVelocityMin, trailVelocityMax, speed);
        grabTrail.time = Mathf.Lerp(trailTimeMin, trailTimeMax, t) * baseTrailTime;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (luggage.isTutorialLuggage) return;
        if (!wasThrown) return;
        if (Time.time < nextHitParticleTime) return;

        float force = collision.impulse.magnitude;
        if (force <= hitParticleForceThreshold) return;

        nextHitParticleTime = Time.time + hitParticleCooldown;
        PlayHitParticle(collision, force);
    }

    private void SilenceEffectsForTutorial()
    {
        tutorialVisualsApplied = true;
        wasGrabbedLastFrame = false;
        wasThrown = false;
        SetTrailActive(false);

        foreach (ParticleSystem particle in hitParticle)
        {
            if (particle == null) continue;
            ParticleSystem.MainModule main = particle.main;
            main.playOnAwake = false;
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void SetTrailActive(bool active)
    {
        if (grabTrail == null) return;
        grabTrail.enabled = active;
        if (!active)
        {
            grabTrail.Clear();
            grabTrail.time = baseTrailTime;
        }
    }

    private void PlayHitParticle(Collision collision, float force)
    {
        if (hitParticle.Count == 0) return;

        float t = Mathf.InverseLerp(particleScaleForceMin, particleScaleForceMax, force);
        float scaledMin = Mathf.Lerp(particleSizeAtMinForce, particleSizeAtMaxForce, t);
        float scaledMax = scaledMin * 2.5f;

        var main = hitParticle[currentHitParticleIndex].main;
        //main.startSize = new ParticleSystem.randomBetweenTwoConstants(scaledMin, scaledMax);
        main.startSize = (scaledMin + scaledMax) / 2f;

        ContactPoint contact = collision.GetContact(0);
        hitParticle[currentHitParticleIndex].transform.position = contact.point;
        hitParticle[currentHitParticleIndex].transform.rotation = Quaternion.LookRotation(contact.normal);
        hitParticle[currentHitParticleIndex].Play();

        currentHitParticleIndex = (currentHitParticleIndex + 1) % hitParticle.Count;
    }
}